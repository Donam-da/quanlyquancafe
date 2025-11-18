using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace namm
{
    // Lớp xử lý logic cho control xem trước hóa đơn.
    public partial class InvoicePreviewControl : UserControl
    {
        // Chuỗi kết nối CSDL.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public InvoicePreviewControl()
        {
            InitializeComponent();
            // Tải và thiết lập ảnh nền mờ khi control được tạo.
            _ = SetBackgroundImageAsync();
        }

        // Phương thức chính để nhận dữ liệu và hiển thị hóa đơn.
        public void DisplayInvoice(DataRowView invoiceData, DataView detailsView)
        {
            // Nếu không có dữ liệu, xóa và ẩn control đi.
            if (invoiceData == null || detailsView == null)
            {
                Clear();
                return;
            }

            // Hiển thị control lên.
            this.Visibility = Visibility.Visible;

            // Điền thông tin chung của hóa đơn vào các TextBlock.
            tbInvoiceId.Text = ((int)invoiceData["ID"]).ToString("D6");
            tbTableName.Text = invoiceData["TableName"].ToString();
            tbCustomerName.Text = invoiceData["CustomerName"].ToString();
            tbCustomerCode.Text = invoiceData["CustomerCode"]?.ToString() ?? "N/A"; // Dùng ?? để xử lý trường hợp giá trị là null.
            tbDateTime.Text = ((DateTime)invoiceData["DateCheckOut"]).ToString("dd/MM/yyyy HH:mm");

            // Gán danh sách các món hàng cho DataGrid.
            dgBillItems.ItemsSource = detailsView;

            // Lấy tổng tiền cuối cùng và tổng tiền gốc (trước giảm giá).
            decimal totalAmount = (decimal)invoiceData["TotalAmount"];
            decimal subTotal = (invoiceData["SubTotal"] != DBNull.Value) ? (decimal)invoiceData["SubTotal"] : totalAmount;

            // Hiển thị các giá trị tiền.
            tbSubTotal.Text = $"{subTotal:N0}";
            tbTotalAmount.Text = $"{totalAmount:N0} VNĐ";

            // Nếu có giảm giá (tổng gốc > tổng cuối).
            if (subTotal > totalAmount && subTotal > 0) // Thêm điều kiện subTotal > 0 để tránh lỗi chia cho 0
            {
                // Tính toán và hiển thị số tiền và phần trăm giảm giá.
                decimal discountAmount = subTotal - totalAmount;
                decimal discountPercent = (discountAmount / subTotal) * 100;
                tbDiscountAmount.Text = $"-{discountAmount:N0} ({discountPercent:G29}%)"; // G29 để loại bỏ các số 0 thừa.
                gridDiscount.Visibility = Visibility.Visible;
            }
            else
            {
                // Ẩn phần giảm giá nếu không có.
                gridDiscount.Visibility = Visibility.Collapsed;
            }
        }

        // Hàm để xóa nội dung và ẩn control đi.
        public void Clear()
        {
            this.Visibility = Visibility.Collapsed;
            dgBillItems.ItemsSource = null;
        }

        // Hàm tải ảnh nền từ CSDL và đặt làm nền mờ cho hóa đơn.
        private async Task SetBackgroundImageAsync()
        {
            try
            {
                byte[]? imageData = null;
                // Truy vấn CSDL để lấy ảnh đang được kích hoạt cho màn hình đăng nhập.
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("SELECT ImageData FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    imageData = await command.ExecuteScalarAsync() as byte[];
                }

                // Nếu có ảnh, tạo một ImageBrush và đặt làm nền cho Grid chính.
                if (imageData != null && imageData.Length > 0)
                {
                    var imageSource = await Task.Run(() => LoadImageFromBytes(imageData));
                    var imageBrush = new ImageBrush(imageSource)
                    {
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.15 // Đặt độ mờ để làm ảnh chìm xuống.
                    };
                    imageBrush.Freeze(); // "Đóng băng" brush để tối ưu hóa hiệu suất.
                    PreviewGrid.Background = imageBrush;
                }
            }
            catch (Exception)
            {
                // Bỏ qua lỗi, không hiển thị ảnh nền nếu có sự cố.
            }
        }

        // Hàm trợ giúp để chuyển đổi một mảng byte thành đối tượng BitmapImage.
        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze(); // "Đóng băng" ảnh để tối ưu hóa hiệu suất.
            return image;
        }
    }
}