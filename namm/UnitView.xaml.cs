﻿// File này chứa logic cho màn hình quản lý Đơn vị tính (UnitView.xaml).
// Chức năng chính bao gồm:
// 1. Hiển thị danh sách các đơn vị tính từ cơ sở dữ liệu.
// 2. Cho phép thêm, sửa, xóa các đơn vị tính.
// 3. Tìm kiếm đơn vị tính theo tên.
// 4. Cập nhật trạng thái (Kích hoạt/Vô hiệu hóa) cho đơn vị tính.
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho UserControl UnitView.
    public partial class UnitView : UserControl
    {
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ và quản lý danh sách các đơn vị tính.
        private DataTable unitDataTable = new DataTable();

        // Hàm khởi tạo của UserControl.
        public UnitView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUnits(); // Tải danh sách đơn vị tính khi màn hình được mở.
        }

        // Hàm tải danh sách các đơn vị tính từ CSDL và hiển thị lên DataGrid.
        private void LoadUnits()
        {
            // 'using' đảm bảo kết nối được đóng lại ngay cả khi có lỗi.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy tất cả thông tin từ bảng Unit.
                const string query = "SELECT ID, Name, Abbreviation, Description, IsActive FROM Unit";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                unitDataTable = new DataTable();
                unitDataTable.Columns.Add("STT", typeof(int)); // Thêm cột "STT" để đánh số thứ tự trên giao diện.
                unitDataTable.Columns.Add("StatusText", typeof(string)); // Thêm cột ảo để hiển thị trạng thái bằng chữ ("Kích hoạt"/"Vô hiệu hóa").
                adapter.Fill(unitDataTable); // Đổ dữ liệu từ CSDL vào bảng tạm (DataTable).

                UpdateStatusText(); // Chuyển đổi giá trị boolean (true/false) của IsActive thành chuỗi.
                dgUnits.ItemsSource = unitDataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgUnits_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1; // Gán số thứ tự bằng chỉ số của dòng + 1.
            }
        }

        // Sự kiện được gọi khi người dùng chọn một dòng khác trong DataGrid.
        private void DgUnits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Kiểm tra xem có dòng nào được chọn không.
            if (dgUnits.SelectedItem is DataRowView row)
            {
                // Điền thông tin của đơn vị tính đã chọn vào các ô trong form bên phải.
                txtName.Text = row["Name"].ToString();
                txtAbbreviation.Text = row["Abbreviation"].ToString();
                txtDescription.Text = row["Description"].ToString();
                chkIsActive.IsChecked = (bool)row["IsActive"];
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thêm".
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem tên đơn vị tính có bị bỏ trống không.
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Tên đơn vị tính không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 'using' đảm bảo kết nối được đóng lại ngay cả khi có lỗi.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để chèn một bản ghi mới vào bảng Unit.
                const string query = "INSERT INTO Unit (Name, Abbreviation, Description, IsActive) VALUES (@Name, @Abbreviation, @Description, @IsActive)";
                SqlCommand command = new SqlCommand(query, connection);
                // Thêm các tham số vào câu lệnh để tránh lỗi SQL Injection.
                command.Parameters.AddWithValue("@Name", txtName.Text);
                // Nếu các ô không bắt buộc bị bỏ trống, chèn giá trị NULL vào CSDL.
                command.Parameters.AddWithValue("@Abbreviation", string.IsNullOrWhiteSpace(txtAbbreviation.Text) ? (object)DBNull.Value : txtAbbreviation.Text);
                command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
                command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);


                connection.Open(); // Mở kết nối.
                command.ExecuteNonQuery(); // Thực thi câu lệnh.
                MessageBox.Show("Thêm đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUnits(); // Tải lại danh sách để hiển thị đơn vị mới.
                ResetFields(); // Làm mới các ô nhập liệu.
            }
        }

        // Xử lý sự kiện khi nhấn nút "Sửa".
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem đã có đơn vị nào được chọn chưa.
            if (dgUnits.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn vị tính để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lấy thông tin của dòng đang được chọn.
            DataRowView row = (DataRowView)dgUnits.SelectedItem;
            int unitId = (int)row["ID"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để cập nhật thông tin của một đơn vị tính dựa vào ID.
                const string query = "UPDATE Unit SET Name = @Name, Abbreviation = @Abbreviation, Description = @Description, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", unitId);
                command.Parameters.AddWithValue("@Name", txtName.Text);
                command.Parameters.AddWithValue("@Abbreviation", string.IsNullOrWhiteSpace(txtAbbreviation.Text) ? (object)DBNull.Value : txtAbbreviation.Text);
                command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
                command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);

                connection.Open(); // Mở kết nối.
                command.ExecuteNonQuery(); // Thực thi câu lệnh.
                MessageBox.Show("Cập nhật đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUnits(); // Tải lại danh sách.
                ResetFields(); // Làm mới các ô nhập liệu.
            }
        }

        // Xử lý sự kiện khi nhấn nút "Xóa".
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgUnits.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn vị tính để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Hiển thị hộp thoại xác nhận trước khi xóa.
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa đơn vị tính này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgUnits.SelectedItem;
                int unitId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    // Câu lệnh SQL để xóa một đơn vị tính dựa vào ID.
                    const string query = "DELETE FROM Unit WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", unitId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUnits(); // Tải lại danh sách.
                    ResetFields(); // Làm mới các ô nhập liệu.
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Làm mới".
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields(); // Gọi hàm để xóa trắng form.
        }

        // Hàm xóa trắng các ô nhập liệu và đặt lại trạng thái mặc định.
        private void ResetFields()
        {
            txtName.Clear();
            txtAbbreviation.Clear();
            txtDescription.Clear();
            chkIsActive.IsChecked = true; // Đặt lại trạng thái "Kích hoạt" là mặc định.
            dgUnits.SelectedItem = null; // Bỏ chọn dòng hiện tại trong DataGrid.
        }

        // Sự kiện được gọi khi người dùng nhập văn bản vào ô tìm kiếm.
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = txtSearch.Text;
            if (unitDataTable != null)
            {
                // Nếu ô tìm kiếm trống, hiển thị lại tất cả các dòng.
                if (string.IsNullOrEmpty(filter))
                {
                    unitDataTable.DefaultView.RowFilter = "";
                }
                else
                {
                    // Áp dụng bộ lọc cho DataView. Nó sẽ ẩn các dòng không chứa chuỗi tìm kiếm trong cột "Name".
                    // Dấu ' được thay thế bằng '' để tránh lỗi cú pháp nếu người dùng nhập dấu nháy đơn.
                    unitDataTable.DefaultView.RowFilter = $"Name LIKE '%{filter.Replace("'", "''")}%'";
                }
            }
        }

        // Hàm duyệt qua DataTable và chuyển đổi giá trị boolean của cột "IsActive" thành chuỗi dễ hiểu.
        private void UpdateStatusText()
        {
            foreach (DataRow row in unitDataTable.Rows)
            {
                // Gán giá trị cho cột ảo "StatusText" dựa trên giá trị của cột "IsActive".
                row["StatusText"] = (bool)row["IsActive"] ? "Kích hoạt" : "Vô hiệu hóa";
            }
        }
    }
}