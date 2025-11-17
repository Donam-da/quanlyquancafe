namespace namm
{
    // Lớp Data Transfer Object (DTO) cho thông tin tài khoản.
    // Dùng để đóng gói và truyền dữ liệu tài khoản giữa các tầng của ứng dụng.
    public class AccountDTO
    {
        // Tên đăng nhập của người dùng.
        public string UserName { get; set; } = string.Empty;
        // Tên hiển thị của người dùng trong giao diện.
        public string DisplayName { get; set; } = string.Empty;
        // Loại tài khoản (ví dụ: 0 là Nhân viên, 1 là Admin).
        public int Type { get; set; }
        // Mật khẩu của người dùng.
        public string Password { get; set; } = string.Empty;
    }
}