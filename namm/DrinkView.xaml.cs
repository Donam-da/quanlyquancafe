﻿﻿﻿﻿﻿using System;
using System.Configuration;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho màn hình quản lý Đồ uống Nguyên bản (DrinkView.xaml).
    public partial class DrinkView : UserControl
    {
        // Chuỗi kết nối CSDL.
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ danh sách đồ uống từ CSDL.
        private DataTable? drinkDataTable;

        public DrinkView()
        {
            InitializeComponent();
            // Đăng ký sự kiện để tải lại dữ liệu mỗi khi view được hiển thị, giúp dữ liệu luôn mới.
            this.IsVisibleChanged += DrinkView_IsVisibleChanged;
        }

        // Hàm này không còn cần thiết vì đã chuyển logic vào IsVisibleChanged.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
        }

        // Sự kiện được gọi mỗi khi UserControl được hiển thị hoặc ẩn đi.
        private async void DrinkView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Nếu UserControl trở nên hiển thị (visible), tải lại dữ liệu.
            if ((bool)e.NewValue)
            {
                await LoadDataAsync();
            }
        }

        // Hàm tổng hợp để tải tất cả dữ liệu cần thiết cho màn hình.
        private async Task LoadDataAsync()
        {
            try
            {
                await LoadDrinksToComboBoxAsync(); // Tải danh sách đồ uống vào ComboBox.
                await LoadDrinksAsync(); // Tải danh sách đồ uống vào DataGrid.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Tải danh sách tất cả đồ uống vào ComboBox để người dùng chọn.
        private async Task LoadDrinksToComboBoxAsync()
        {
            const string query = "SELECT ID, Name, DrinkCode FROM Drink ORDER BY Name";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable drinkListTable = new DataTable();
                await Task.Run(() => adapter.Fill(drinkListTable));
                cbDrink.ItemsSource = drinkListTable.DefaultView; // Gán dữ liệu cho ComboBox.
            }
        }

        // Tải danh sách các đồ uống nguyên bản (có giá nhập hoặc tồn kho) vào DataGrid.
        private async Task LoadDrinksAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy các đồ uống có thể bán nguyên bản.
                string query = @"
                    SELECT 
                        d.ID, 
                        CASE WHEN d.DrinkCode IS NOT NULL THEN (d.DrinkCode + '_NB') ELSE '' END AS DrinkCode, 
                        d.Name, d.OriginalPrice, d.ActualPrice, d.StockQuantity, d.CategoryID,
                        ISNULL(c.Name, 'N/A') AS CategoryName 
                    FROM Drink d
                    LEFT JOIN Category c ON d.CategoryID = c.ID
                    WHERE d.ID IN (SELECT DISTINCT DrinkID FROM BillInfo WHERE DrinkType = N'Nguyên bản') OR d.OriginalPrice > 0 OR d.StockQuantity > 0";

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                drinkDataTable = new DataTable();
                drinkDataTable.Columns.Add("STT", typeof(int));
                drinkDataTable.Columns.Add("StatusText", typeof(string));
                await Task.Run(() => adapter.Fill(drinkDataTable));

                // Cập nhật cột "StatusText" dựa trên giá nhập.
                foreach (DataRow row in drinkDataTable.Rows)
                {
                    bool isActive = Convert.ToDecimal(row["OriginalPrice"]) > 0;
                    row["StatusText"] = isActive ? "Hoạt động" : "Đã ẩn"; // Nếu giá nhập > 0, coi là đang hoạt động.
                }

                dgDrinks.ItemsSource = drinkDataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgDrinks_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1; // Gán số thứ tự.
            }
        }

        // Sự kiện được gọi khi người dùng chọn một dòng trong DataGrid.
        private void DgDrinks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Khi chọn một dòng, hiển thị thông tin của nó lên form bên phải.
            if (dgDrinks.SelectedItem is DataRowView row)
            {
                cbDrink.SelectionChanged -= CbDrink_SelectionChanged; // Tạm ngắt sự kiện để tránh vòng lặp.
                cbDrink.SelectedValue = row["ID"]; // Đồng bộ lựa chọn lên ComboBox.
                cbDrink.SelectionChanged += CbDrink_SelectionChanged; // Bật lại sự kiện.

                // Điền thông tin vào các ô nhập liệu.
                txtDrinkCode.Text = row["DrinkCode"] as string ?? string.Empty;
                txtPrice.Text = Convert.ToDecimal(row["OriginalPrice"]).ToString("G0"); // Bỏ phần thập phân .00
                txtActualPrice.Text = Convert.ToDecimal(row["ActualPrice"]).ToString("G0"); // Bỏ phần thập phân .00

                // Cập nhật nội dung và tooltip của nút Ẩn/Hiện.
                bool isHidden = Convert.ToDecimal(row["OriginalPrice"]) == 0;
                btnHide.Content = isHidden ? "Hiện" : "Ẩn";
                btnHide.ToolTip = isHidden ? "Kích hoạt lại đồ uống này để bán dưới dạng nguyên bản." : "Ẩn đồ uống này khỏi danh sách bán nguyên bản.";

                txtStockQuantity.Text = Convert.ToDecimal(row["StockQuantity"]).ToString("G0");
                cbDrink.IsEnabled = false; // Không cho đổi đồ uống khi đang ở chế độ sửa.
                btnHide.IsEnabled = true; // Bật các nút chức năng.
                btnDelete.IsEnabled = true;
            }
        }

        // Xử lý khi nhấn nút "Lưu".
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để gán thuộc tính.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return; // Kiểm tra dữ liệu nhập có hợp lệ không.

            int drinkId = (int)cbDrink.SelectedValue;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh UPDATE để cập nhật giá và tồn kho.
                const string query = "UPDATE Drink SET OriginalPrice = @OriginalPrice, ActualPrice = @ActualPrice, StockQuantity = @StockQuantity WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command, drinkId); // Thêm các tham số vào câu lệnh.
                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    _ = LoadDrinksAsync(); // Tải lại danh sách để cập nhật giao diện.
                    ResetFields(); // Làm mới form.
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý khi nhấn nút "Xóa".
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgDrinks.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgDrinks.SelectedItem is DataRowView row)
            {
                int drinkId = (int)row["ID"];
                string drinkName = row["Name"].ToString() ?? "Không tên";

                // Kiểm tra xem đồ uống có tồn tại trong lịch sử giao dịch không.
                bool isInBill = false;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    const string checkQuery = "SELECT TOP 1 1 FROM BillInfo WHERE DrinkID = @DrinkID";
                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@DrinkID", drinkId);
                    await connection.OpenAsync();
                    var result = await checkCommand.ExecuteScalarAsync();
                    if (result != null)
                    {
                        isInBill = true;
                    }
                }

                // Nếu đã có giao dịch, không cho phép xóa.
                if (isInBill)
                {
                    MessageBox.Show($"Không thể xóa đồ uống '{drinkName}' vì đã có lịch sử giao dịch. Bạn có thể ẩn đồ uống này thay thế.", "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Hiển thị hộp thoại xác nhận trước khi xóa vĩnh viễn.
                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa vĩnh viễn đồ uống '{drinkName}' không? Hành động này sẽ xóa cả công thức pha chế (nếu có) và không thể hoàn tác.", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        const string deleteQuery = "DELETE FROM Recipe WHERE DrinkID = @ID; DELETE FROM Drink WHERE ID = @ID;";
                        SqlCommand command = new SqlCommand(deleteQuery, connection);
                        command.Parameters.AddWithValue("@ID", drinkId);
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        MessageBox.Show("Xóa đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadDataAsync(); // Tải lại toàn bộ dữ liệu.
                        ResetFields(); // Làm mới form.
                    }
                }
            }
        }

        // Xử lý khi nhấn nút "Ẩn" hoặc "Hiện".
        private async void BtnHide_Click(object sender, RoutedEventArgs e)
        {
            if (dgDrinks.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để ẩn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgDrinks.SelectedItem is DataRowView row)
            {
                int drinkId = (int)row["ID"];
                string drinkName = row["Name"].ToString() ?? "Không tên";

                bool isCurrentlyHidden = Convert.ToDecimal(row["OriginalPrice"]) == 0;
                string actionText = isCurrentlyHidden ? "hiển thị lại" : "ẩn";
                string confirmationMessage = isCurrentlyHidden
                    ? $"Bạn có muốn hiển thị lại đồ uống '{drinkName}' để bán dưới dạng nguyên bản không?"
                    : $"Bạn có chắc chắn muốn ẩn đồ uống '{drinkName}' khỏi danh sách đồ uống nguyên bản không? Đồ uống này sẽ không còn được bán dưới dạng nguyên bản nữa nhưng vẫn có thể bán dưới dạng pha chế nếu có công thức.";

                if (MessageBox.Show(confirmationMessage, "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // Nếu hành động là "Hiện", chỉ thông báo và yêu cầu người dùng nhập giá mới rồi nhấn Lưu.
                    if (isCurrentlyHidden)
                    {
                        MessageBox.Show($"Để kích hoạt lại '{drinkName}', vui lòng nhập giá nhập mới và nhấn 'Lưu'.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtPrice.Focus(); // Đặt con trỏ vào ô nhập giá.
                        txtPrice.SelectAll();
                        return;
                    }

                    // Nếu hành động là "Ẩn", cập nhật OriginalPrice về 0 trong CSDL.
                    try
                    {
                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            const string updateQuery = "UPDATE Drink SET OriginalPrice = 0 WHERE ID = @ID;";
                            SqlCommand command = new SqlCommand(updateQuery, connection);
                            command.Parameters.AddWithValue("@ID", drinkId);

                            await connection.OpenAsync();
                            await command.ExecuteNonQueryAsync();
                        }

                        MessageBox.Show($"Đã ẩn đồ uống '{drinkName}' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadDataAsync(); // Tải lại dữ liệu để cập nhật giao diện.
                        ResetFields(); // Làm mới form.
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show($"Lỗi khi ẩn đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Hàm này không còn cần thiết vì logic đã được tích hợp trong LoadDrinksAsync.
        private void UpdateStatusText()
        {
            if (drinkDataTable == null) return;
            foreach (DataRow row in drinkDataTable.Rows)
            {
                // Nếu OriginalPrice > 0 thì là "Hoạt động", ngược lại là "Ẩn"
                row["StatusText"] = (Convert.ToDecimal(row["OriginalPrice"]) > 0) ? "Hoạt động" : "Đã ẩn";
            }
        }

        // Xử lý khi nhấn nút "Làm mới".
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        // Sự kiện được gọi mỗi khi nội dung trong ô tìm kiếm thay đổi.
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (drinkDataTable != null)
            {
                // Lọc các dòng trong DataGrid dựa trên văn bản tìm kiếm.
                drinkDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        // Sự kiện được gọi khi người dùng chọn một đồ uống từ ComboBox.
        private void CbDrink_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDrink.SelectedItem is DataRowView selectedDrink && drinkDataTable != null)
            {
                int selectedId = (int)selectedDrink["ID"];

                // Tìm kiếm xem đồ uống này đã có trong danh sách quản lý (DataGrid) chưa.
                DataRow? existingDrinkRow = drinkDataTable.AsEnumerable()
                    .FirstOrDefault(row => (int)row["ID"] == selectedId);

                // Nếu đã có, hiển thị thông tin của nó.
                if (existingDrinkRow != null)
                {
                    txtDrinkCode.Text = existingDrinkRow["DrinkCode"] as string ?? string.Empty;
                    txtPrice.Text = Convert.ToDecimal(existingDrinkRow["OriginalPrice"]).ToString("G0");
                    txtActualPrice.Text = Convert.ToDecimal(existingDrinkRow["ActualPrice"]).ToString("G0");
                    txtStockQuantity.Text = Convert.ToDecimal(existingDrinkRow["StockQuantity"]).ToString("G0");
                }
                else
                {
                    // Nếu là đồ uống chưa được quản lý, tạo mã mới và xóa các trường khác.
                    txtDrinkCode.Text = (selectedDrink["DrinkCode"] as string ?? "") + "_NB";
                    txtPrice.Clear();
                    txtActualPrice.Clear();
                    txtStockQuantity.Text = "0"; // Mặc định tồn kho là 0
                }
            }
        }

        // Hàm để xóa trắng các ô nhập liệu và đặt lại trạng thái các nút.
        private void ResetFields()
        {
            cbDrink.SelectedIndex = -1;
            txtDrinkCode.Clear();
            txtPrice.Clear();
            txtActualPrice.Clear();
            txtStockQuantity.Clear();
            dgDrinks.SelectedItem = null;
            cbDrink.IsEnabled = true; // Cho phép chọn đồ uống mới.
            btnHide.Content = "Ẩn";
            btnHide.IsEnabled = false; // Tắt các nút chức năng.
            btnDelete.IsEnabled = false;
        }

        // Hàm kiểm tra dữ liệu đầu vào.
        private bool ValidateInput()
        {
            if (cbDrink.SelectedItem == null || string.IsNullOrWhiteSpace(txtPrice.Text) || 
                string.IsNullOrWhiteSpace(txtActualPrice.Text) || string.IsNullOrWhiteSpace(txtStockQuantity.Text))
            {
                MessageBox.Show("Vui lòng chọn đồ uống và nhập đầy đủ giá nhập, giá bán và số lượng tồn kho.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtPrice.Text, out _) || !decimal.TryParse(txtActualPrice.Text, out _) || !decimal.TryParse(txtStockQuantity.Text, out _))
            {
                MessageBox.Show("Giá nhập, giá bán và số lượng tồn kho phải là số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
            command.Parameters.AddWithValue("@OriginalPrice", Convert.ToDecimal(txtPrice.Text));
            command.Parameters.AddWithValue("@ActualPrice", Convert.ToDecimal(txtActualPrice.Text));
            command.Parameters.AddWithValue("@StockQuantity", Convert.ToDecimal(txtStockQuantity.Text));
        }
    }
}