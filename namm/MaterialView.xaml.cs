﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho màn hình quản lý Nguyên liệu (MaterialView.xaml).
    public partial class MaterialView : UserControl
    {
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ danh sách các nguyên liệu từ CSDL.
        private DataTable materialDataTable = new DataTable();

        // Hàm khởi tạo của UserControl.
        public MaterialView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUnitsToComboBox(); // Tải danh sách đơn vị tính vào ComboBox.
            LoadMaterials(); // Tải danh sách nguyên liệu vào DataGrid.
        }

        // Tải danh sách các đơn vị tính (Unit) đang hoạt động vào ComboBox.
        private void LoadUnitsToComboBox()
        {
            // 'using' đảm bảo kết nối sẽ được đóng lại ngay cả khi có lỗi.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy ID và Tên của các đơn vị đang hoạt động.
                string query = "SELECT ID, Name FROM Unit WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable unitTable = new DataTable();
                adapter.Fill(unitTable); // Đổ dữ liệu vào bảng tạm.
                cbUnit.ItemsSource = unitTable.DefaultView; // Gán dữ liệu cho ComboBox.
            }
        }

        // Tải danh sách các nguyên liệu từ CSDL.
        private void LoadMaterials()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL dùng JOIN để lấy tên đơn vị tính (UnitName) từ bảng Unit.
                string query = @"
                    SELECT 
                        m.ID, m.Name, m.Quantity, m.Price, m.Description, m.IsActive, m.UnitID,
                        u.Name AS UnitName 
                    FROM Material m
                    LEFT JOIN Unit u ON m.UnitID = u.ID";

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                materialDataTable = new DataTable(); // Tạo mới bảng dữ liệu.
                materialDataTable.Columns.Add("STT", typeof(int)); // Thêm cột tạm thời để đánh số thứ tự.
                materialDataTable.Columns.Add("StatusText", typeof(string)); // Thêm cột tạm thời để hiển thị trạng thái.
                adapter.Fill(materialDataTable); // Đổ dữ liệu từ CSDL vào bảng.

                UpdateStatusText(); // Chuyển đổi giá trị true/false thành chuỗi "Sử dụng"/"Ngưng".
                dgMaterials.ItemsSource = materialDataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Cập nhật cột "StatusText" dựa trên giá trị của cột "IsActive".
        private void UpdateStatusText()
        {
            foreach (DataRow row in materialDataTable.Rows)
            {
                // Chuyển đổi giá trị boolean (true/false) thành chuỗi để hiển thị.
                row["StatusText"] = (bool)row["IsActive"] ? "Sử dụng" : "Ngưng";
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgMaterials_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1; // Gán số thứ tự cho cột STT.
            }
        }

        // Sự kiện được gọi khi người dùng chọn một dòng trong DataGrid.
        private void DgMaterials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nếu có một dòng được chọn.
            if (dgMaterials.SelectedItem is DataRowView row)
            {
                // Hiển thị thông tin của dòng đó lên các ô nhập liệu.
                txtName.Text = row["Name"].ToString(); // Tên nguyên liệu.
                txtQuantity.Text = row["Quantity"].ToString(); // Số lượng tồn.
                txtPrice.Text = Convert.ToDecimal(row["Price"]).ToString("G0"); // Đơn giá, bỏ phần thập phân .00.
                txtDescription.Text = row["Description"].ToString(); // Ghi chú.
                chkIsActive.IsChecked = (bool)row["IsActive"]; // Trạng thái.

                // Tìm và chọn đơn vị tính tương ứng trong ComboBox.
                cbUnit.SelectedValue = row["UnitID"];
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thêm".
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return; // Kiểm tra dữ liệu đầu vào trước khi thêm.

            // Thực hiện thêm mới vào CSDL.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO Material (Name, UnitID, Quantity, Price, Description, IsActive) VALUES (@Name, @UnitID, @Quantity, @Price, @Description, @IsActive)";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command);

                try
                {
                    connection.Open();
                    command.ExecuteNonQuery(); // Thực thi câu lệnh.
                    MessageBox.Show("Thêm nguyên liệu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadMaterials(); // Tải lại danh sách để cập nhật giao diện.
                    ResetFields(); // Làm mới form.
                }
                catch (SqlException ex)
                {
                    // Bắt lỗi SQL, ví dụ như tên nguyên liệu đã tồn tại (lỗi khóa UNIQUE).
                    MessageBox.Show($"Lỗi khi thêm nguyên liệu: {ex.Message}\n\nCó thể tên nguyên liệu này đã tồn tại.", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Sửa".
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterials.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nguyên liệu để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return; // Kiểm tra dữ liệu đầu vào.

            // Lấy ID của nguyên liệu đang được chọn.
            DataRowView row = (DataRowView)dgMaterials.SelectedItem;
            int materialId = (int)row["ID"];

            // Thực hiện cập nhật trong CSDL.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Material SET Name = @Name, UnitID = @UnitID, Quantity = @Quantity, Price = @Price, Description = @Description, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", materialId);
                AddParameters(command);

                connection.Open();
                command.ExecuteNonQuery(); // Thực thi câu lệnh.
                MessageBox.Show("Cập nhật nguyên liệu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadMaterials(); // Tải lại danh sách.
                ResetFields(); // Làm mới form.
            }
        }

        // Xử lý sự kiện khi nhấn nút "Xóa".
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterials.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nguyên liệu để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Hiển thị hộp thoại xác nhận trước khi xóa.
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa nguyên liệu này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgMaterials.SelectedItem;
                int materialId = (int)row["ID"];

                // Thực hiện xóa trong CSDL.
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "DELETE FROM Material WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", materialId);
                    connection.Open();
                    command.ExecuteNonQuery();

                    MessageBox.Show("Xóa nguyên liệu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadMaterials(); // Tải lại danh sách.
                    ResetFields(); // Làm mới form.
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
            // Lọc các dòng trong DataGrid dựa trên văn bản tìm kiếm ở cột "Name".
            materialDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
        }

        // Hàm để xóa trắng các ô nhập liệu và đặt lại trạng thái các nút.
        private void ResetFields()
        {
            txtName.Clear();
            txtQuantity.Clear();
            txtPrice.Clear();
            txtDescription.Clear();
            cbUnit.SelectedIndex = -1; // Bỏ chọn ComboBox.
            chkIsActive.IsChecked = true;
            dgMaterials.SelectedItem = null;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || cbUnit.SelectedItem == null || string.IsNullOrWhiteSpace(txtQuantity.Text) || string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                // Kiểm tra các trường bắt buộc.
                MessageBox.Show("Tên, đơn vị, số lượng và đơn giá không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtQuantity.Text, out _) || !decimal.TryParse(txtPrice.Text, out _))
            {
                // Kiểm tra xem số lượng và đơn giá có phải là số hợp lệ không.
                MessageBox.Show("Số lượng và đơn giá phải là số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        // Hàm trợ giúp để thêm các tham số vào SqlCommand, tránh lặp lại code.
        private void AddParameters(SqlCommand command)
        {
            // Thêm các tham số từ các ô nhập liệu vào câu lệnh SQL.
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@UnitID", cbUnit.SelectedValue);
            command.Parameters.AddWithValue("@Quantity", Convert.ToDecimal(txtQuantity.Text));
            command.Parameters.AddWithValue("@Price", Convert.ToDecimal(txtPrice.Text));
            // Nếu ô ghi chú trống, lưu giá trị NULL vào CSDL.
            command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
            // Lấy giá trị từ CheckBox, nếu nó không được check (null), coi như là false.
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }
    }
}