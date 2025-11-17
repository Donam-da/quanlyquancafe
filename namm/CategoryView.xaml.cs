﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace namm
{
    // Lớp xử lý logic cho màn hình quản lý Loại đồ uống (CategoryView.xaml).
    public partial class CategoryView : UserControl
    {
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ danh sách các loại đồ uống từ CSDL.
        private DataTable? categoryDataTable;

        // Hàm khởi tạo của UserControl.
        public CategoryView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadCategoriesAsync(); // Tải danh sách loại đồ uống một cách bất đồng bộ.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            ResetFields(); // Đặt lại trạng thái của form nhập liệu.
        }

        // Tải danh sách các loại đồ uống từ CSDL.
        private async Task LoadCategoriesAsync()
        {
            // 'using' đảm bảo kết nối sẽ được đóng lại ngay cả khi có lỗi.
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, IsActive FROM Category";
                var adapter = new SqlDataAdapter(query, connection);
                categoryDataTable = new DataTable();
                // Thêm 2 cột tạm thời vào DataTable để hiển thị trên giao diện.
                categoryDataTable.Columns.Add("STT", typeof(int));
                categoryDataTable.Columns.Add("StatusText", typeof(string));

                try
                {
                    // Chạy tác vụ lấy dữ liệu trên một luồng nền để không làm treo giao diện.
                    await Task.Run(() => adapter.Fill(categoryDataTable));
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi SQL khi tải danh mục: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                UpdateStatusText(); // Chuyển đổi giá trị true/false thành chuỗi "Sử dụng"/"Ngưng".
                dgCategories.ItemsSource = categoryDataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Cập nhật cột "StatusText" dựa trên giá trị của cột "IsActive".
        private void UpdateStatusText()
        {
            if (categoryDataTable == null) return;
            foreach (DataRow row in categoryDataTable.Rows)
            {   // Chuyển đổi giá trị boolean (true/false) thành chuỗi để hiển thị.
                row["StatusText"] = (row["IsActive"] != DBNull.Value && (bool)row["IsActive"]) ? "Sử dụng" : "Ngưng";
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgCategories_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1; // Gán số thứ tự cho cột STT.
            }
        }

        // Sự kiện được gọi khi người dùng chọn một dòng trong DataGrid.
        private void DgCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nếu có một dòng được chọn.
            if (dgCategories.SelectedItem is DataRowView row)
            {
                // Hiển thị thông tin của dòng đó lên các ô nhập liệu.
                txtName.Text = row["Name"].ToString();
                chkIsActive.IsChecked = (bool)row["IsActive"];

                // Vô hiệu hóa nút "Thêm", bật nút "Sửa" và "Xóa".
                btnAdd.IsEnabled = false;
                btnEdit.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
            else
            {
                ResetFields(); // Nếu không có dòng nào được chọn, làm mới form.
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thêm".
        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem tên loại đồ uống có được nhập hay không.
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Tên loại đồ uống không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Thực hiện thêm mới vào CSDL.
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO Category (Name, IsActive) VALUES (@Name, @IsActive)";
                var command = new SqlCommand(query, connection);
                AddParameters(command); // Thêm các tham số vào câu lệnh SQL.

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Thêm loại đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCategoriesAsync(); // Tải lại danh sách để cập nhật giao diện.
                    ResetFields(); // Làm mới form.
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Sửa".
        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategories.SelectedItem == null) return;
            // Kiểm tra dữ liệu nhập.
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Tên loại đồ uống không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Lấy ID của loại đồ uống đang được chọn.
            var row = (DataRowView)dgCategories.SelectedItem;
            int categoryId = (int)row["ID"];

            // Thực hiện cập nhật trong CSDL.
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "UPDATE Category SET Name = @Name, IsActive = @IsActive WHERE ID = @ID";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", categoryId);
                AddParameters(command); // Thêm các tham số vào câu lệnh SQL.

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Cập nhật thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCategoriesAsync(); // Tải lại danh sách.
                    ResetFields(); // Làm mới form.
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Xóa".
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategories.SelectedItem == null) return;

            // Hiển thị hộp thoại xác nhận trước khi xóa.
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa loại đồ uống này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Lấy ID của loại đồ uống đang được chọn.
                var row = (DataRowView)dgCategories.SelectedItem;
                int categoryId = (int)row["ID"];

                // Thực hiện xóa trong CSDL.
                using (var connection = new SqlConnection(connectionString))
                {
                    const string query = "DELETE FROM Category WHERE ID = @ID";
                    var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", categoryId);
                    try
                    {
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        MessageBox.Show("Xóa thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadCategoriesAsync(); // Tải lại danh sách.
                        ResetFields(); // Làm mới form.
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show($"Lỗi khi xóa: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Làm mới".
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        // Hàm để xóa trắng các ô nhập liệu và đặt lại trạng thái các nút.
        private void ResetFields()
        {
            txtName.Clear();
            chkIsActive.IsChecked = true;
            dgCategories.SelectedItem = null;
            // Bật nút "Thêm", tắt nút "Sửa" và "Xóa".
            btnAdd.IsEnabled = true;
            btnEdit.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }

        // Sự kiện được gọi mỗi khi nội dung trong ô tìm kiếm thay đổi.
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (categoryDataTable != null)
            {
                // Lọc các dòng trong DataGrid dựa trên văn bản tìm kiếm.
                categoryDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        // Hàm trợ giúp để thêm các tham số vào SqlCommand, tránh lặp lại code.
        private void AddParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }
    }
}