﻿﻿﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho màn hình quản lý Nhân viên (EmployeeView.xaml).
    public partial class EmployeeView : UserControl
    {
        // Chuỗi kết nối CSDL.
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public EmployeeView()
        {
            InitializeComponent();
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAccounts(); // Tải danh sách tài khoản từ CSDL.
        }

        // Tải danh sách các tài khoản từ CSDL vào DataGrid.
        private void LoadAccounts()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT UserName, DisplayName, Password, Type, PhoneNumber, Address FROM Account";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("STT", typeof(int)); // Thêm cột tạm thời để đánh số thứ tự.
                adapter.Fill(dataTable);

                dgAccounts.ItemsSource = dataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Sự kiện được gọi khi người dùng chọn một dòng trong DataGrid.
        private void DgAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nếu có một dòng được chọn.
            if (dgAccounts.SelectedItem is DataRowView row)
            {
                // Hiển thị thông tin của dòng đó lên các ô nhập liệu.
                txtUserName.Text = row["UserName"]?.ToString();
                txtDisplayName.Text = row["DisplayName"]?.ToString();
                txtPhoneNumber.Text = row["PhoneNumber"]?.ToString();
                txtAddress.Text = row["Address"]?.ToString();
                cbAccountType.SelectedIndex = Convert.ToInt32(row["Type"]);
                // Cảnh báo: Hiển thị mật khẩu dạng plain text là không an toàn.
                txtPassword.Text = row["Password"]?.ToString();
                txtUserName.IsEnabled = false; // Không cho sửa tên đăng nhập (vì là khóa chính).
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgAccounts_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1; // Gán số thứ tự.
            }
        }

        // Xử lý khi nhấn nút "Thêm".
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Lấy dữ liệu từ các ô nhập liệu.
            string userName = txtUserName.Text;
            string displayName = txtDisplayName.Text;
            string password = txtPassword.Text;
            string phone = txtPhoneNumber.Text;
            string address = txtAddress.Text;
            int type = Convert.ToInt32(((ComboBoxItem)cbAccountType.SelectedItem).Tag);

            // Kiểm tra dữ liệu đầu vào.
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Tên đăng nhập, tên hiển thị và mật khẩu không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Thực hiện thêm mới vào CSDL.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO Account (UserName, DisplayName, Password, Type, PhoneNumber, Address) VALUES (@UserName, @DisplayName, @Password, @Type, @PhoneNumber, @Address)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@DisplayName", displayName);
                command.Parameters.AddWithValue("@Password", password); // Cảnh báo: Lưu mật khẩu plain text là không an toàn.
                command.Parameters.AddWithValue("@Type", type);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(phone) ? (object)DBNull.Value : phone);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(address) ? (object)DBNull.Value : address);

                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Thêm nhân viên thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAccounts(); // Tải lại danh sách để cập nhật giao diện.
                    ResetFields(); // Làm mới form.
                }
                catch (SqlException ex)
                {
                    MessageBox.Show("Lỗi khi thêm nhân viên: " + ex.Message, "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý khi nhấn nút "Sửa".
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgAccounts.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nhân viên để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lấy dữ liệu từ các ô nhập liệu.
            string userName = txtUserName.Text;
            string displayName = txtDisplayName.Text;
            string password = txtPassword.Text;
            string phone = txtPhoneNumber.Text;
            string address = txtAddress.Text;
            int type = Convert.ToInt32(((ComboBoxItem)cbAccountType.SelectedItem).Tag);

            // Thực hiện cập nhật trong CSDL.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Account SET DisplayName=@DisplayName, Password=@Password, Type=@Type, PhoneNumber=@PhoneNumber, Address=@Address WHERE UserName=@UserName";

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@DisplayName", displayName);
                // Yêu cầu mật khẩu không được để trống khi cập nhật.
                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Mật khẩu không được để trống khi cập nhật.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                command.Parameters.AddWithValue("@Password", password);
                command.Parameters.AddWithValue("@Type", type);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(phone) ? (object)DBNull.Value : phone);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(address) ? (object)DBNull.Value : address);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật thông tin thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAccounts(); // Tải lại danh sách.
                ResetFields(); // Làm mới form.
            }
        }

        // Xử lý khi nhấn nút "Xóa".
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgAccounts.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nhân viên để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Hiển thị hộp thoại xác nhận trước khi xóa.
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa nhân viên này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string userName = ((DataRowView)dgAccounts.SelectedItem)["UserName"].ToString();
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    // Thực hiện xóa trong CSDL.
                    string query = "DELETE FROM Account WHERE UserName=@UserName";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@UserName", userName);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa nhân viên thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAccounts();
                    ResetFields();
                }
            }
        }

        // Xử lý khi nhấn nút "Làm mới".
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        // Hàm để xóa trắng các ô nhập liệu và đặt lại trạng thái các nút.
        private void ResetFields()
        {
            txtUserName.Clear();
            txtDisplayName.Clear();
            txtPassword.Clear();
            txtPhoneNumber.Clear();
            txtAddress.Clear();
            cbAccountType.SelectedIndex = 0;
            dgAccounts.SelectedItem = null;
            txtUserName.IsEnabled = true; // Cho phép nhập tên đăng nhập mới.
        }
    }
}