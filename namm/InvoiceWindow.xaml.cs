// File này chứa logic xử lý cho cửa sổ InvoiceWindow.xaml.
// Chức năng chính là nhận dữ liệu hóa đơn từ màn hình trước, hiển thị lên giao diện,
// và xử lý các hành động của người dùng như xác nhận thanh toán hoặc in hóa đơn.
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace namm
{
    // Lớp InvoiceWindow đại diện cho cửa sổ hóa đơn.
    public partial class InvoiceWindow : Window
    {
        // Chuỗi kết nối đến cơ sở dữ liệu, được lấy từ file cấu hình App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        // Hàm khởi tạo (constructor) của cửa sổ hóa đơn.
        // Nó nhận tất cả thông tin cần thiết để hiển thị một hóa đơn hoàn chỉnh.
        public InvoiceWindow(int tableId, string tableName, string customerName, string customerCode, decimal subTotal, decimal discountPercent, decimal finalTotal, ObservableCollection<BillItem> billItems, int billId)
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.

            // Tải và thiết lập ảnh nền một cách bất đồng bộ (không chờ đợi hoàn thành).
            _ = SetBackgroundImageAsync();

            // Điền các thông tin chung của hóa đơn vào các TextBlock tương ứng trên giao diện.
            tbInvoiceId.Text = billId.ToString("D6"); // Định dạng số hóa đơn thành 6 chữ số, ví dụ: 000123.
            tbTableName.Text = tableName; // Tên bàn.
            tbCustomerCode.Text = customerCode; // Mã khách hàng.
            tbCustomerName.Text = customerName; // Tên khách hàng.
            tbDateTime.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm"); // Ngày giờ hiện tại.

            // Định dạng và hiển thị các thông tin về tiền tệ.
            tbSubTotal.Text = $"{subTotal:N0}"; // Tổng tiền hàng (trước giảm giá), định dạng có dấu phẩy.
            tbTotalAmount.Text = $"{finalTotal:N0} VNĐ"; // Tổng tiền cuối cùng phải trả.

            // Kiểm tra nếu có giảm giá thì mới hiển thị phần thông tin giảm giá.
            if (discountPercent > 0)
            {
                decimal discountAmount = subTotal - finalTotal; // Tính số tiền được giảm.
                tbDiscountAmount.Text = $"-{discountAmount:N0} ({discountPercent:G29}%)"; // Hiển thị cả số tiền và phần trăm giảm giá.
                gridDiscount.Visibility = Visibility.Visible; // Cho phép phần giảm giá hiện ra.
            }

            // Gán danh sách các món hàng (billItems) cho DataGrid để hiển thị.
            dgBillItems.ItemsSource = billItems;
        }

        // Phương thức bất đồng bộ để tải và đặt ảnh nền cho hóa đơn.
        private async Task SetBackgroundImageAsync()
        {
            try
            {
                byte[]? imageData = null;
                // Sử dụng 'using' để đảm bảo kết nối được đóng lại sau khi hoàn tất.
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(); // Mở kết nối đến CSDL một cách bất đồng bộ.
                    // Câu lệnh SQL để lấy dữ liệu ảnh (ImageData) từ bảng InterfaceImages, nơi ảnh được đánh dấu là active.
                    var command = new SqlCommand("SELECT ImageData FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    imageData = await command.ExecuteScalarAsync() as byte[]; // Thực thi và lấy kết quả.
                }

                // Nếu có dữ liệu ảnh, tiến hành tạo và áp dụng ảnh nền.
                if (imageData != null && imageData.Length > 0)
                {
                    // Chuyển đổi mảng byte thành đối tượng BitmapImage mà WPF có thể sử dụng.
                    var imageSource = await Task.Run(() => LoadImageFromBytes(imageData));
                    // Tạo một ImageBrush từ ảnh đã tải.
                    var imageBrush = new ImageBrush(imageSource)
                    {
                        Stretch = Stretch.UniformToFill, // Căng ảnh để lấp đầy nền.
                        Opacity = 0.15 // Đặt độ mờ để làm ảnh chìm, không che mất nội dung.
                    };
                    imageBrush.Freeze(); // "Đóng băng" brush để tối ưu hóa hiệu suất, ngăn không cho nó thay đổi.
                    InvoiceGrid.Background = imageBrush; // Đặt brush làm nền cho Grid chính của hóa đơn.
                }
                // Nếu không tìm thấy ảnh, Grid sẽ giữ nền trong suốt mặc định của nó.
            }
            catch (Exception)
            {
                // Nếu có bất kỳ lỗi nào xảy ra (ví dụ: không kết nối được CSDL), sẽ bỏ qua và không đặt ảnh nền.
            }
        }

        // Hàm trợ giúp để chuyển đổi một mảng byte (dữ liệu ảnh) thành đối tượng BitmapImage.
        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            var image = new BitmapImage();
            // Sử dụng MemoryStream để đọc dữ liệu ảnh từ mảng byte trong bộ nhớ.
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0; // Đặt con trỏ về đầu stream.
                image.BeginInit(); // Bắt đầu quá trình khởi tạo ảnh.
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat; // Giữ nguyên định dạng pixel của ảnh gốc.
                image.CacheOption = BitmapCacheOption.OnLoad; // Tải toàn bộ ảnh vào bộ nhớ ngay lập tức.
                image.StreamSource = mem; // Nguồn dữ liệu của ảnh là MemoryStream.
                image.EndInit(); // Kết thúc quá trình khởi tạo.
            }
            image.Freeze(); // "Đóng băng" ảnh để tối ưu hóa hiệu suất.
            return image; // Trả về đối tượng ảnh đã tạo.
        }

        // Xử lý sự kiện khi người dùng nhấn nút "Xác nhận thanh toán".
        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // Đặt DialogResult thành true. Cửa sổ gọi ShowDialog() sẽ nhận được kết quả này,
            // cho biết người dùng đã xác nhận thanh toán.
            this.DialogResult = true;
        }

        // Xử lý sự kiện khi người dùng nhấn nút "In hóa đơn".
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Hiện tại, chức năng này chỉ là một placeholder, hiển thị một thông báo.
            MessageBox.Show("Chức năng in hóa đơn đang được phát triển!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

            // Đoạn code dưới đây được comment lại, là ví dụ về cách triển khai chức năng in thực tế.
            // Nó sử dụng PrintDialog của WPF để mở hộp thoại in của hệ thống.
            // PrintDialog printDialog = new PrintDialog();
            // if (printDialog.ShowDialog() == true)
            // {
            //     // In toàn bộ giao diện của cửa sổ này (this) với tiêu đề "In hóa đơn".
            //     printDialog.PrintVisual(this, "In hóa đơn"); 
            // }
        }
    }
}