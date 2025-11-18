﻿﻿﻿﻿﻿﻿﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho màn hình quản lý Menu đồ uống (MenuView.xaml).
    public partial class MenuView : UserControl
    {
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ danh sách các đồ uống từ CSDL.
        private DataTable? menuDataTable;

        // Hàm khởi tạo của UserControl.
        public MenuView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCategoriesToComboBox(); // Tải danh sách loại đồ uống vào ComboBox.
            await LoadMenuItems(); // Tải danh sách đồ uống vào DataGrid.
        }

        // Tải danh sách các loại đồ uống (Category) đang hoạt động vào ComboBox.
        private async Task LoadCategoriesToComboBox()
        {
            // 'using' đảm bảo kết nối sẽ được đóng lại ngay cả khi có lỗi.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable)); // Đổ dữ liệu vào bảng tạm trên một luồng nền.
                cbCategory.ItemsSource = categoryTable.DefaultView; // Gán dữ liệu cho ComboBox.
            }
        }

        // Tải danh sách tất cả đồ uống từ CSDL.
        private async Task LoadMenuItems()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy thông tin đồ uống, bao gồm cả tên loại và xác định "Kiểu đồ uống".
                const string query = @"
                    SELECT 
                        d.ID, 
                        d.DrinkCode, 
                        d.Name, 
                        d.IsActive, 
                        d.CategoryID, 
                        c.Name AS CategoryName, -- Lấy tên loại từ bảng Category.
                        -- Logic để xác định kiểu đồ uống dựa trên giá nhập và sự tồn tại của công thức.
                        CASE 
                            WHEN d.OriginalPrice > 0 AND EXISTS (SELECT 1 FROM Recipe r WHERE r.DrinkID = d.ID) THEN N'Nguyên bản/Pha chế'
                            WHEN d.OriginalPrice > 0 THEN N'Nguyên bản'
                            WHEN EXISTS (SELECT 1 FROM Recipe r WHERE r.DrinkID = d.ID) THEN N'Pha chế'
                            ELSE N'Chưa gán' 
                        END AS DrinkType
                    FROM Drink d 
                    LEFT JOIN Category c ON d.CategoryID = c.ID ORDER BY d.Name"; // Lấy tất cả đồ uống, kể cả đồ uống bị ẩn.

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                menuDataTable = new DataTable(); // Tạo mới bảng dữ liệu.
                menuDataTable.Columns.Add("STT", typeof(int)); // Thêm cột tạm thời để đánh số thứ tự.
                menuDataTable.Columns.Add("StatusText", typeof(string)); // Thêm cột tạm thời để hiển thị trạng thái.
                await Task.Run(() => adapter.Fill(menuDataTable)); // Đổ dữ liệu từ CSDL vào bảng.

                UpdateStatusText(); // Chuyển đổi giá trị true/false thành chuỗi "Hiển thị"/"Ẩn".
                dgMenuItems.ItemsSource = menuDataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Cập nhật cột "StatusText" dựa trên giá trị của cột "IsActive".
        private void UpdateStatusText()
        {
            if (menuDataTable != null) foreach (DataRow row in menuDataTable.Rows)
            {
                // Chuyển đổi giá trị boolean (true/false) thành chuỗi để hiển thị.
                row["StatusText"] = (bool)row["IsActive"] ? "Hiển thị" : "Ẩn";
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgMenuItems_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        // Sự kiện được gọi khi người dùng chọn một dòng trong DataGrid.
        private void DgMenuItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nếu có một dòng được chọn.
            if (dgMenuItems.SelectedItem is DataRowView row)
            {
                // Tạm ngắt sự kiện LostFocus để tránh tự động tạo mã mới khi chỉ đang chọn một món đã có.
                txtName.LostFocus -= TxtName_LostFocus;

                // Hiển thị thông tin của dòng đó lên các ô nhập liệu.
                txtName.Text = row["Name"] as string ?? string.Empty;
                txtDrinkCode.Text = row["DrinkCode"] as string ?? string.Empty;
                cbCategory.SelectedValue = row["CategoryID"]; // Chọn loại đồ uống tương ứng trong ComboBox.
                chkIsActive.IsChecked = Convert.ToBoolean(row["IsActive"]);

                // Bật lại sự kiện LostFocus.
                txtName.LostFocus += TxtName_LostFocus;

                // Cập nhật trạng thái các nút: Tắt nút "Thêm", Bật các nút "Sửa", "Ẩn".
                btnAdd.IsEnabled = false;
                btnUpdate.IsEnabled = true;
                btnHide.IsEnabled = true;
            }
            else
            {
                ResetFields();
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thêm".
        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return; // Kiểm tra dữ liệu đầu vào trước khi thêm.

            // Thực hiện thêm mới vào CSDL.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để thêm một đồ uống mới, giá và chi phí ban đầu được đặt là 0.
                const string query = "INSERT INTO Drink (DrinkCode, Name, CategoryID, IsActive, OriginalPrice, RecipeCost, ActualPrice) VALUES (@DrinkCode, @Name, @CategoryID, @IsActive, 0, 0, 0)";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command);

                try
                {
                    await connection.OpenAsync(); // Mở kết nối.
                    await command.ExecuteNonQueryAsync(); // Thực thi câu lệnh.
                    MessageBox.Show("Thêm đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadMenuItems(); // Tải lại danh sách để cập nhật giao diện.
                    ResetFields(); // Làm mới form.
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Sửa".
        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dgMenuItems.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để cập nhật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return; // Kiểm tra dữ liệu đầu vào.

            // Lấy thông tin của đồ uống đang được chọn.
            if (dgMenuItems.SelectedItem is DataRowView row)
            {
                int drinkId = (int)row["ID"];

                // Thực hiện cập nhật trong CSDL.
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    // Câu lệnh chỉ cập nhật Tên, Loại và Trạng thái. Mã đồ uống không được thay đổi.
                    const string query = "UPDATE Drink SET Name = @Name, CategoryID = @CategoryID, IsActive = @IsActive WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    AddParameters(command, drinkId);

                    try
                    {
                        await connection.OpenAsync(); // Mở kết nối.
                        await command.ExecuteNonQueryAsync(); // Thực thi câu lệnh.
                        MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadMenuItems(); // Tải lại danh sách.
                        ResetFields(); // Làm mới form.
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show($"Lỗi khi cập nhật đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Ẩn" (Soft Delete).
        private async void BtnHide_Click(object sender, RoutedEventArgs e)
        {
            if (dgMenuItems.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để ẩn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Hiển thị hộp thoại xác nhận trước khi ẩn.
            if (MessageBox.Show("Bạn có chắc chắn muốn ẩn đồ uống này? Đồ uống sẽ không còn hiển thị trên menu bán hàng nhưng vẫn giữ lại lịch sử giao dịch.", "Xác nhận ẩn", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (dgMenuItems.SelectedItem is DataRowView row)
                {
                    int drinkId = (int)row["ID"];

                    // Thực hiện cập nhật trong CSDL.
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        // Chỉ cập nhật cột IsActive thành 0 (false) thay vì xóa.
                        const string query = "UPDATE Drink SET IsActive = 0 WHERE ID = @ID";
                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@ID", drinkId);

                        await connection.OpenAsync(); // Mở kết nối.
                        await command.ExecuteNonQueryAsync(); // Thực thi câu lệnh.
                        MessageBox.Show("Ẩn đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadMenuItems(); // Tải lại danh sách.
                        ResetFields(); // Làm mới form.
                    }
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Xóa" (Hard Delete).
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgMenuItems.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgMenuItems.SelectedItem is DataRowView row)
            {
                int drinkId = (int)row["ID"];
                string drinkName = row["Name"].ToString() ?? "Không tên";

                // Bước 1: Kiểm tra xem đồ uống có tồn tại trong bất kỳ hóa đơn nào không.
                bool isInBill = false;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    const string checkQuery = "SELECT TOP 1 1 FROM BillInfo WHERE DrinkID = @DrinkID";
                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@DrinkID", drinkId);
                    await connection.OpenAsync(); // Mở kết nối.
                    var result = await checkCommand.ExecuteScalarAsync();
                    if (result != null)
                    {
                        isInBill = true;
                    }
                }

                // Nếu đồ uống đã có trong hóa đơn, không cho phép xóa.
                if (isInBill)
                {
                    MessageBox.Show($"Không thể xóa đồ uống '{drinkName}' vì đã có lịch sử giao dịch. Bạn có thể ẩn đồ uống này thay thế.", "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Bước 2: Hiển thị hộp thoại xác nhận xóa vĩnh viễn.
                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa vĩnh viễn đồ uống '{drinkName}' không? Hành động này sẽ xóa cả công thức pha chế (nếu có) và không thể hoàn tác.", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // Bước 3: Thực hiện xóa trong CSDL.
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        // Xóa các bản ghi liên quan trong bảng Recipe trước, sau đó mới xóa trong bảng Drink.
                        const string deleteQuery = "DELETE FROM Recipe WHERE DrinkID = @ID; DELETE FROM Drink WHERE ID = @ID;";
                        SqlCommand command = new SqlCommand(deleteQuery, connection);
                        command.Parameters.AddWithValue("@ID", drinkId);
                        await connection.OpenAsync(); // Mở kết nối.
                        await command.ExecuteNonQueryAsync(); // Thực thi câu lệnh.
                        MessageBox.Show("Xóa đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadMenuItems(); // Tải lại danh sách.
                        ResetFields(); // Làm mới form.
                    }
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Làm mới".
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        // Sự kiện được gọi mỗi khi nội dung trong ô tìm kiếm thay đổi.
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (menuDataTable != null)
            {
                // Lọc các dòng trong DataGrid dựa trên văn bản tìm kiếm ở cột "Name".
                menuDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        // Sự kiện được gọi khi người dùng rời khỏi ô nhập tên đồ uống.
        private void TxtName_LostFocus(object sender, RoutedEventArgs e)
        {
            // Chỉ tự động tạo mã khi người dùng đang thêm mới (chưa chọn item nào từ grid).
            if (dgMenuItems.SelectedItem == null)
            {
                txtDrinkCode.Text = GenerateMenuCode(txtName.Text);
            }
        }

        // Hàm tạo mã đồ uống tự động từ tên.
        private string GenerateMenuCode(string drinkName)
        {
            // Chuyển tên đồ uống thành mã không dấu, không khoảng trắng, không ký tự đặc biệt.
            string temp = drinkName.ToLower();
            temp = Regex.Replace(temp, "[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            temp = Regex.Replace(temp, "[éèẻẽẹêếềểễệ]", "e");
            temp = Regex.Replace(temp, "[íìỉĩị]", "i");
            temp = Regex.Replace(temp, "[óòỏõọôốồổỗộơớờởỡợ]", "o");
            temp = Regex.Replace(temp, "[úùủũụưứừửữự]", "u");
            temp = Regex.Replace(temp, "[ýỳỷỹỵ]", "y");
            temp = Regex.Replace(temp, "[đ]", "d"); // Chuyển 'đ' thành 'd'.
            // Bỏ các ký tự đặc biệt và khoảng trắng, chỉ giữ lại chữ cái và số.
            temp = Regex.Replace(temp.Replace(" ", ""), "[^a-z0-9]", "");
            return temp;
        }

        // Hàm kiểm tra dữ liệu đầu vào.
        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || cbCategory.SelectedItem == null || string.IsNullOrWhiteSpace(txtDrinkCode.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin: Tên và loại đồ uống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        // Hàm trợ giúp để thêm các tham số vào SqlCommand, tránh lặp lại code.
        private void AddParameters(SqlCommand command, int? id = null)
        {
            if (id.HasValue)
            {
                command.Parameters.AddWithValue("@ID", id.Value);
            }
            command.Parameters.AddWithValue("@DrinkCode", txtDrinkCode.Text);
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@CategoryID", cbCategory.SelectedValue);
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }

        // Hàm để xóa trắng các ô nhập liệu và đặt lại trạng thái các nút.
        private void ResetFields()
        {
            txtName.Clear();
            txtDrinkCode.Clear();
            cbCategory.SelectedIndex = -1; // Bỏ chọn ComboBox.
            chkIsActive.IsChecked = true;
            dgMenuItems.SelectedItem = null;

            // Đặt lại trạng thái các nút: Bật nút "Thêm", Tắt các nút "Sửa", "Ẩn".
            btnAdd.IsEnabled = true;
            btnUpdate.IsEnabled = false;
            btnHide.IsEnabled = false;
        }
    }
}