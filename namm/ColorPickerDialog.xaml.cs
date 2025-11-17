using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace namm
{
    // Lớp xử lý logic cho cửa sổ ColorPickerDialog.xaml.
    public partial class ColorPickerDialog : Window
    {
        // Thuộc tính để lưu và trả về màu mà người dùng đã chọn.
        public Color SelectedColor { get; private set; }

        // Hàm khởi tạo, nhận vào một màu ban đầu để hiển thị.
        public ColorPickerDialog(Color initialColor)
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
            SelectedColor = initialColor; // Gán màu ban đầu.
            // Thiết lập giá trị ban đầu cho các thanh trượt dựa trên màu được truyền vào.
            sliderRed.Value = initialColor.R;
            sliderGreen.Value = initialColor.G;
            sliderBlue.Value = initialColor.B;
            UpdateColorPreview(); // Cập nhật ô xem trước màu.
        }

        // Sự kiện được gọi mỗi khi giá trị của bất kỳ thanh trượt nào thay đổi.
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateColorPreview(); // Cập nhật lại màu xem trước.
        }

        // Hàm cập nhật màu sắc dựa trên giá trị hiện tại của các thanh trượt.
        private void UpdateColorPreview()
        {
            // Tạo một đối tượng màu mới từ giá trị của 3 thanh trượt.
            SelectedColor = Color.FromRgb((byte)sliderRed.Value, (byte)sliderGreen.Value, (byte)sliderBlue.Value);
            // Cập nhật màu nền cho ô xem trước.
            colorPreview.Fill = new SolidColorBrush(SelectedColor);
        }

        // Xử lý khi người dùng nhấn nút "OK".
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true; // Đặt kết quả là 'true' và đóng cửa sổ.
        }

        // Xử lý khi người dùng nhấn nút "Hủy".
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // Đặt kết quả là 'false' và đóng cửa sổ.
        }

        // Xử lý khi cửa sổ được tải xong.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Tự động đặt con trỏ vào thanh trượt đầu tiên để cải thiện trải nghiệm người dùng.
            sliderRed.Focus();
        }
    }
}