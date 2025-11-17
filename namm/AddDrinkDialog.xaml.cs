using System.Windows;
using System.Windows.Input;

namespace namm
{
    // Lớp xử lý logic cho cửa sổ AddDrinkDialog.xaml (nhập số lượng đồ uống).
    public partial class AddDrinkDialog : Window
    {
        // Thuộc tính để lưu trữ số lượng người dùng đã nhập.
        public int Quantity { get; private set; }

        // Hàm khởi tạo, nhận tên đồ uống để hiển thị.
        public AddDrinkDialog(string drinkName)
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
            tbDrinkName.Text = drinkName; // Gán tên đồ uống nhận được vào TextBlock để hiển thị.
        }

        // Xử lý khi nhấn nút "Đồng ý".
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra và chuyển đổi số lượng nhập vào.
            if (int.TryParse(txtQuantity.Text, out int quantity) && quantity > 0)
            {
                this.Quantity = quantity; // Lưu lại số lượng.
                this.DialogResult = true; // Đặt kết quả là 'true' và đóng cửa sổ.
            }
            else
            {
                // Hiển thị lỗi nếu số lượng không hợp lệ.
                MessageBox.Show("Số lượng phải là một số nguyên dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Xử lý khi cửa sổ được tải xong.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtQuantity.Focus(); // Tự động đặt con trỏ chuột vào ô nhập số lượng.
            txtQuantity.SelectAll(); // Bôi đen toàn bộ nội dung trong ô (số "1" mặc định) để người dùng có thể nhập đè lên ngay.
        }

        // Xử lý khi nhấn phím trong ô số lượng.
        private void TxtQuantity_KeyDown(object sender, KeyEventArgs e)
        {
            // Nếu nhấn Enter, coi như nhấn nút "Đồng ý".
            if (e.Key == Key.Enter)
            {
                BtnOk_Click(sender, e); // Gọi hàm xử lý của nút "Đồng ý" để xác nhận và đóng cửa sổ.
            }
        }
    }
}