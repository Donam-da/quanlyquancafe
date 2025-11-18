﻿﻿// File này chứa logic cho màn hình quản lý bàn (TableView.xaml).
// Chức năng chính bao gồm:
// 1. Hiển thị danh sách các bàn từ cơ sở dữ liệu.
// 2. Cho phép thêm một bàn mới.
// 3. Cho phép sửa thông tin của một bàn đã có (tên, sức chứa, trạng thái).
// 4. Cho phép xóa một bàn khỏi hệ thống.
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho UserControl TableView.
    public partial class TableView : UserControl
    {
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        // Hàm khởi tạo của UserControl.
        public TableView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTables(); // Tải danh sách bàn khi màn hình được mở.
        }

        // Hàm tải danh sách các bàn từ CSDL và hiển thị lên DataGrid.
        private void LoadTables()
        {
            // 'using' đảm bảo kết nối được đóng lại ngay cả khi có lỗi.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy các thông tin cần thiết của bàn.
                const string query = "SELECT ID, Name, Capacity, Status FROM TableFood";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("STT", typeof(int)); // Thêm cột "STT" để đánh số thứ tự trên giao diện.
                adapter.Fill(dataTable); // Đổ dữ liệu từ CSDL vào bảng tạm (DataTable).
                dgTables.ItemsSource = dataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgTables_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1; // Gán số thứ tự bằng chỉ số của dòng + 1.
            }
        }

        // Sự kiện được gọi khi người dùng chọn một dòng khác trong DataGrid.
        private void DgTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Kiểm tra xem có dòng nào được chọn không.
            if (dgTables.SelectedItem is DataRowView row)
            {
                // Lấy tên đầy đủ của bàn (ví dụ: "Bàn 10").
                string fullName = row["Name"].ToString() ?? "";
                // Chỉ lấy phần số của tên bàn để hiển thị trong ô nhập liệu cho gọn.
                string tableNumber = fullName.Replace("Bàn ", "").Trim();
                txtName.Text = tableNumber;

                // Điền các thông tin khác của bàn đã chọn vào các ô tương ứng.
                txtCapacity.Text = row["Capacity"].ToString();
                cbStatus.Text = row["Status"].ToString();

                // Khi chọn một bàn, chuyển sang chế độ Sửa/Xóa: tắt nút "Thêm", bật nút "Sửa" và "Xóa".
                btnAdd.IsEnabled = false;
                btnEdit.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
            else
            {
                ResetFields(); // Nếu không có dòng nào được chọn (ví dụ khi nhấn nút "Làm mới"), reset các ô nhập liệu.
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thêm".
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem mã bàn nhập vào có phải là số hay không.
            if (!int.TryParse(txtName.Text, out _))
            {
                MessageBox.Show("Mã bàn phải là một số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 'using' đảm bảo kết nối được đóng lại ngay cả khi có lỗi.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để chèn một bản ghi mới vào bảng TableFood.
                const string query = "INSERT INTO TableFood (Name, Capacity, Status) VALUES (@Name, @Capacity, @Status)";
                SqlCommand command = new SqlCommand(query, connection);
                // Tự động thêm tiền tố "Bàn " vào trước số người dùng nhập để tạo tên bàn hoàn chỉnh.
                command.Parameters.AddWithValue("@Name", "Bàn " + txtName.Text);
                command.Parameters.AddWithValue("@Capacity", Convert.ToInt32(txtCapacity.Text));
                command.Parameters.AddWithValue("@Status", ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString());

                try
                {
                    connection.Open(); // Mở kết nối.
                    command.ExecuteNonQuery(); // Thực thi câu lệnh.
                    MessageBox.Show("Thêm bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTables(); // Tải lại danh sách bàn để hiển thị bàn mới.
                    ResetFields(); // Làm mới các ô nhập liệu.
                }
                catch (SqlException ex)
                {
                    // Bắt lỗi SQL, ví dụ như lỗi tên bàn đã tồn tại (do ràng buộc UNIQUE trong CSDL).
                    MessageBox.Show($"Lỗi khi thêm bàn: {ex.Message}\n\nCó thể tên bàn này đã tồn tại.", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Sửa".
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem đã có bàn nào được chọn chưa.
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtName.Text, out _))
            {
                MessageBox.Show("Mã bàn phải là một số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Lấy thông tin của dòng đang được chọn.
            DataRowView row = (DataRowView)dgTables.SelectedItem;
            int tableId = (int)row["ID"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để cập nhật thông tin của một bàn dựa vào ID.
                const string query = "UPDATE TableFood SET Name = @Name, Capacity = @Capacity, Status = @Status WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", tableId);
                // Tự động thêm "Bàn " vào trước số người dùng nhập
                command.Parameters.AddWithValue("@Name", "Bàn " + txtName.Text);
                command.Parameters.AddWithValue("@Capacity", Convert.ToInt32(txtCapacity.Text));
                command.Parameters.AddWithValue("@Status", ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString());

                connection.Open(); // Mở kết nối.
                command.ExecuteNonQuery(); // Thực thi câu lệnh.
                MessageBox.Show("Cập nhật thông tin bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadTables(); // Tải lại danh sách bàn.
                ResetFields(); // Làm mới các ô nhập liệu.
            }
        }

        // Xử lý sự kiện khi nhấn nút "Xóa".
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Hiển thị hộp thoại xác nhận trước khi xóa.
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa bàn này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgTables.SelectedItem;
                int tableId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    // Câu lệnh SQL để xóa một bàn dựa vào ID.
                    const string query = "DELETE FROM TableFood WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", tableId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTables(); // Tải lại danh sách bàn.
                    ResetFields(); // Làm mới các ô nhập liệu.
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Làm mới".
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields(); // Gọi hàm để xóa trắng form và reset trạng thái nút.
        }

        // Hàm xóa trắng các ô nhập liệu và đặt lại trạng thái các nút về chế độ "Thêm mới".
        private void ResetFields()
        {
            // Xóa trắng các ô nhập liệu.
            txtName.Clear();
            txtCapacity.Clear();
            cbStatus.SelectedIndex = 0; // Đặt ComboBox về lựa chọn đầu tiên ("Trống").
            dgTables.SelectedItem = null; // Bỏ chọn dòng hiện tại trong DataGrid.

            // Khi làm mới, chuyển về chế độ "Thêm": bật nút "Thêm", tắt nút "Sửa" và "Xóa".
            btnAdd.IsEnabled = true;
            btnEdit.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }
    }
}