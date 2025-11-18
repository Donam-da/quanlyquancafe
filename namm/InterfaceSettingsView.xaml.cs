using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace namm
{
    // Lớp để đại diện cho một ảnh đã lưu trong thư viện CSDL.
    public class SavedImage
    {
        public int ID { get; set; }
        public string ImageName { get; set; } = string.Empty;
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public BitmapImage? Thumbnail { get; set; } // Ảnh thu nhỏ để hiển thị nhanh trên UI.
    }

    // Lớp xử lý logic cho màn hình Cài đặt Giao diện.
    public partial class InterfaceSettingsView : UserControl
    {
        // Chuỗi kết nối CSDL.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Biến lưu màu đang được chọn cho nền ứng dụng và nền icon.
        private Color selectedAppColor;
        private Color selectedLoginPanelColor;

        // Dữ liệu cho ảnh MỚI được tải lên từ máy tính.
        private byte[]? _selectedImageData;
        private string? _selectedImageFileName;
        // ID của ảnh ĐÃ LƯU được chọn từ thư viện (ListView).
        private int? _selectedSavedImageId;

        public InterfaceSettingsView()
        {
            InitializeComponent();
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettingsAsync(); // Tải các cài đặt hiện tại.
            PopulateColorPalette(appColorPalette, AppColor_Click); // Tạo bảng màu cho nền ứng dụng.
            PopulateColorPalette(loginPanelColorPalette, LoginPanelColor_Click); // Tạo bảng màu cho nền icon.
            LoadSavedImagesAsync(); // Tải thư viện ảnh đã lưu từ CSDL.
        }

        // Hàm tạo các ô màu nhỏ để người dùng chọn nhanh.
        private void PopulateColorPalette(Panel palette, RoutedEventHandler colorClickHandler)
        {
            List<Color> colors = new List<Color>
            {
                Colors.LightCoral, Colors.Khaki, Colors.LightGreen, Colors.PaleTurquoise, 
                Colors.LightSteelBlue, Colors.Plum, Colors.LightGray, Colors.MistyRose
            };

            // Duyệt qua danh sách màu và tạo các ô Border tương ứng.
            foreach (var color in colors)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(color),
                    Width = 20, Height = 20, Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                border.MouseLeftButtonDown += (s, e) => colorClickHandler(s, e); // Gán sự kiện click.
                border.Tag = color; // Gắn đối tượng màu vào Tag để lấy ra khi click.
                palette.Children.Add(border);
            }
        }

        // Xử lý khi click vào một ô màu trong bảng màu nền ứng dụng.
        private void AppColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Color color)
            {
                selectedAppColor = color;
                UpdateAppColor(); // Cập nhật màu xem trước.
            }
        }

        // Xử lý khi click vào một ô màu trong bảng màu nền icon.
        private void LoginPanelColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Color color)
            {
                selectedLoginPanelColor = color;
                UpdateLoginPanelColor(); // Cập nhật màu xem trước.
            }
        }

        // Xử lý khi người dùng thay đổi thanh trượt độ sáng/trong suốt của màu nền ứng dụng.
        private void AppColorAdjustment_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
            {
                UpdateAppColor(); // Cập nhật lại màu xem trước.
            }
        }

        // Xử lý khi người dùng thay đổi thanh trượt độ sáng/trong suốt của màu nền icon.
        private void LoginPanelColorAdjustment_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
            {
                UpdateLoginPanelColor(); // Cập nhật lại màu xem trước.
            }
        }

        // Cập nhật màu xem trước cho nền ứng dụng.
        private void UpdateAppColor()
        {
            Color adjustedColor = AdjustColor(selectedAppColor, sliderAppLightness.Value, sliderAppAlpha.Value);
            previewGroupBox.Background = new SolidColorBrush(adjustedColor);
            rectAppColorPreview.Fill = new SolidColorBrush(adjustedColor);
            txtAppBackgroundColorHex.Text = adjustedColor.ToString(); // Hiển thị mã màu Hex.
        }

        // Cập nhật màu xem trước cho nền icon.
        private void UpdateLoginPanelColor()
        {
            Color adjustedColor = AdjustColor(selectedLoginPanelColor, sliderLoginPanelLightness.Value, sliderLoginPanelAlpha.Value);
            previewIconBorder.Background = new SolidColorBrush(adjustedColor);
            rectLoginPanelColorPreview.Fill = new SolidColorBrush(adjustedColor);
            txtLoginPanelBackgroundColorHex.Text = adjustedColor.ToString(); // Hiển thị mã màu Hex.
        }

        // Hàm điều chỉnh màu sắc dựa trên độ sáng và độ trong suốt.
        private Color AdjustColor(Color baseColor, double lightness, double alpha)
        {
            float factor = (float)(1 + lightness);
            byte r = (byte)Math.Max(0, Math.Min(255, baseColor.R * factor));
            byte g = (byte)Math.Max(0, Math.Min(255, baseColor.G * factor));
            byte b = (byte)Math.Max(0, Math.Min(255, baseColor.B * factor));
            byte a = (byte)Math.Max(0, Math.Min(255, 255 * alpha));
            return Color.FromArgb(a, r, g, b);
        }

        // Xử lý khi người dùng nhấn Enter trong ô nhập mã màu Hex.
        private void HexColor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (sender is TextBox textBox)
                {
                    UpdateColorFromHex(textBox); // Cập nhật màu từ chuỗi Hex.
                    e.Handled = true;
                    // Di chuyển focus ra khỏi TextBox để người dùng thấy kết quả ngay.
                    textBox.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                }
            }
        }

        // Xử lý khi người dùng rời khỏi ô nhập mã màu Hex.
        private void HexColor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                UpdateColorFromHex(textBox); // Cập nhật màu từ chuỗi Hex.
            }
        }

        // Cập nhật màu từ chuỗi Hex người dùng nhập.
        private void UpdateColorFromHex(TextBox textBox)
        {
            try
            {
                var newColor = (Color)ColorConverter.ConvertFromString(textBox.Text);

                // Xác định ô nào đã thay đổi và cập nhật màu tương ứng.
                if (textBox.Name == "txtAppBackgroundColorHex")
                {
                    selectedAppColor = newColor;
                    UpdateAppColor();
                }
                else if (textBox.Name == "txtLoginPanelBackgroundColorHex")
                {
                    selectedLoginPanelColor = newColor;
                    UpdateLoginPanelColor();
                }
            }
            catch (FormatException)
            {
                // Nếu định dạng không hợp lệ, hoàn nguyên về giá trị cũ.
                if (textBox.Name == "txtAppBackgroundColorHex")
                {
                    textBox.Text = txtAppBackgroundColorHex.Text;
                }
                else if (textBox.Name == "txtLoginPanelBackgroundColorHex")
                {
                    textBox.Text = txtLoginPanelBackgroundColorHex.Text;
                }
            }
        }

        // Xử lý khi nhấn nút "Chọn ảnh từ máy".
        private async void BtnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Đọc dữ liệu ảnh vào mảng byte và lưu lại.
                    _selectedImageData = await Task.Run(() => File.ReadAllBytes(openFileDialog.FileName));
                    _selectedImageFileName = Path.GetFileName(openFileDialog.FileName);

                    // Cập nhật UI để xem trước.
                    txtImagePath.Text = openFileDialog.FileName;
                    imgPreview.Source = await Task.Run(() => LoadImageFromBytes(_selectedImageData));

                    // Reset lựa chọn ảnh đã lưu vì ta đang ưu tiên ảnh mới.
                    _selectedSavedImageId = null;
                    lvSavedImages.SelectedItem = null;
                    btnAddImageToLibrary.IsEnabled = true; // Bật nút "Thêm vào thư viện".
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đọc file ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    _selectedImageData = null; _selectedImageFileName = null;
                }
            }
        }

        private async void BtnAddImageToLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedImageData == null || string.IsNullOrEmpty(_selectedImageFileName))
            {
                MessageBox.Show("Vui lòng chọn một ảnh từ máy tính trước khi thêm vào thư viện.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Bạn có muốn thêm ảnh '{_selectedImageFileName}' vào thư viện ảnh không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                // Lưu ảnh vào CSDL.
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var cmdInsert = new SqlCommand("INSERT INTO InterfaceImages (ImageName, ImageData, ContentType, IsActiveForLogin) OUTPUT INSERTED.ID VALUES (@Name, @Data, @Type, 0)", connection);
                    cmdInsert.Parameters.AddWithValue("@Name", _selectedImageFileName);
                    cmdInsert.Parameters.AddWithValue("@Data", _selectedImageData);
                    cmdInsert.Parameters.AddWithValue("@Type", GetMimeType(_selectedImageFileName));
                    cmdInsert.CommandTimeout = 120;

                    int newImageId = (int)await cmdInsert.ExecuteScalarAsync();

                    MessageBox.Show($"Ảnh '{_selectedImageFileName}' đã được thêm vào thư viện thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Xóa dữ liệu ảnh mới đã chọn.
                    _selectedImageData = null;
                    _selectedImageFileName = null;
                    txtImagePath.Text = "(Chưa chọn ảnh nào)";
                    btnAddImageToLibrary.IsEnabled = false;

                    // Tải lại thư viện ảnh để hiển thị ảnh mới.
                    await LoadSavedImagesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm ảnh vào thư viện: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Xử lý khi nhấn nút "Lưu cài đặt".
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Lưu các cài đặt màu sắc vào Properties.Settings.
                Properties.Settings.Default.AppBackgroundColor = txtAppBackgroundColorHex.Text;
                Properties.Settings.Default.LoginIconBgColor = txtLoginPanelBackgroundColorHex.Text;

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Sử dụng Transaction để đảm bảo các lệnh được thực thi cùng nhau.
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. Bỏ kích hoạt tất cả các ảnh đăng nhập cũ.
                        var cmdDeactivate = new SqlCommand("UPDATE InterfaceImages SET IsActiveForLogin = 0 WHERE IsActiveForLogin = 1", connection, transaction);
                            cmdDeactivate.CommandTimeout = 120;
                        await cmdDeactivate.ExecuteNonQueryAsync();

                        // 2. Xử lý logic lưu ảnh
                        if (_selectedImageData != null && _selectedImageFileName != null)
                        {
                            // Trường hợp 1: Người dùng tải lên ảnh MỚI -> Lưu và kích hoạt nó.
                            var cmdInsert = new SqlCommand("INSERT INTO InterfaceImages (ImageName, ImageData, ContentType, IsActiveForLogin) OUTPUT INSERTED.ID VALUES (@Name, @Data, @Type, 1)", connection, transaction);
                            cmdInsert.Parameters.AddWithValue("@Name", _selectedImageFileName);
                            cmdInsert.CommandTimeout = 120;
                            cmdInsert.Parameters.AddWithValue("@Data", _selectedImageData);
                            cmdInsert.Parameters.AddWithValue("@Type", GetMimeType(_selectedImageFileName));
                            var newImageId = (int)await cmdInsert.ExecuteScalarAsync();
                            
                            await LoadSavedImagesAsync();
                        }
                        else if (_selectedSavedImageId.HasValue)
                        {
                            // Trường hợp 2: Người dùng chọn ảnh ĐÃ CÓ -> Kích hoạt nó.
                            var cmdActivate = new SqlCommand("UPDATE InterfaceImages SET IsActiveForLogin = 1 WHERE ID = @ID", connection, transaction);
                            cmdActivate.CommandTimeout = 120;
                            cmdActivate.Parameters.AddWithValue("@ID", _selectedSavedImageId.Value);
                            await cmdActivate.ExecuteNonQueryAsync();
                        }
                        // Trường hợp 3: Người dùng không thay đổi ảnh, chỉ đổi màu -> Không cần làm gì thêm.

                        transaction.Commit();

                        // Reset các biến tạm sau khi lưu.
                        _selectedImageData = null;
                        _selectedImageFileName = null;
                    }
                }

                Properties.Settings.Default.Save();
                MessageBox.Show("Đã lưu cài đặt thành công! Vui lòng khởi động lại ứng dụng để các thay đổi có hiệu lực.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu cài đặt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Xử lý khi nhấn nút "Đặt lại mặc định".
        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bạn có chắc chắn muốn đặt lại tất cả cài đặt giao diện về giá trị mặc định không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Properties.Settings.Default.Reset(); // Đặt lại file user.config.
                Properties.Settings.Default.Save();

                // Xóa ảnh đang active trong CSDL.
                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        var cmdDeactivate = new SqlCommand("UPDATE InterfaceImages SET IsActiveForLogin = 0 WHERE IsActiveForLogin = 1", connection);
                        await cmdDeactivate.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex)
                {
                     MessageBox.Show($"Không thể đặt lại ảnh trong CSDL: {ex.Message}", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await LoadCurrentSettingsAsync(); // Tải lại giao diện với cài đặt mặc định.
                MessageBox.Show("Cài đặt đã được đặt lại về mặc định.", "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Tải các cài đặt hiện tại (màu sắc, ảnh active) để hiển thị.
        private async Task LoadCurrentSettingsAsync()
        {
            try
            {
                // Tải màu.
                var appBgColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.AppBackgroundColor);
                var loginPanelColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.LoginIconBgColor);
                selectedAppColor = appBgColor; selectedLoginPanelColor = loginPanelColor;
                UpdateAppColor();
                UpdateLoginPanelColor();

                // Tải ảnh đang được kích hoạt từ CSDL.
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("SELECT ImageData, ImageName FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    command.CommandTimeout = 120;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var imageData = (byte[])reader["ImageData"];
                            var imageName = reader["ImageName"].ToString();
                            imgPreview.Source = await Task.Run(() => LoadImageFromBytes(imageData));
                            txtImagePath.Text = $"Ảnh đang dùng từ CSDL: {imageName}";
                        }
                        else
                        {
                            // Nếu không có ảnh trong CSDL, dùng ảnh mặc định.
                            imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                            txtImagePath.Text = "(Chưa có ảnh nào được thiết lập)";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load settings, using defaults. Error: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                selectedAppColor = Colors.LightGray;
                selectedLoginPanelColor = (Color)ColorConverter.ConvertFromString("#D2B48C");
                UpdateAppColor();
                UpdateLoginPanelColor();
            }
        }

        // Tải danh sách các ảnh đã lưu từ CSDL vào thư viện.
        private async Task LoadSavedImagesAsync()
        {
            var savedImages = new ObservableCollection<SavedImage>();
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("SELECT ID, ImageName, ImageData FROM InterfaceImages ORDER BY DateCreated DESC", connection);
                    command.CommandTimeout = 120;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var imageData = (byte[])reader["ImageData"];
                            var savedImage = new SavedImage
                            {
                                ID = reader.GetInt32(0),
                                ImageName = reader.GetString(1),
                                ImageData = imageData,
                                Thumbnail = await Task.Run(() => LoadImageFromBytes(imageData)) // Tạo thumbnail trên luồng nền để không làm treo UI.
                            };
                            savedImages.Add(savedImage);
                        }
                    }
                }
                lvSavedImages.ItemsSource = savedImages;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải danh sách ảnh đã lưu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Xử lý khi người dùng chọn một ảnh từ thư viện.
        private async void LvSavedImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nếu chỉ chọn 1 ảnh.
            if (lvSavedImages.SelectedItems.Count == 1 && lvSavedImages.SelectedItem is SavedImage selected)
            {
                // Hiển thị ảnh đã chọn lên khung xem trước.
                imgPreview.Source = selected.Thumbnail;
                _selectedSavedImageId = selected.ID;
                txtImagePath.Text = $"Đã chọn ảnh từ CSDL: {selected.ImageName}";
                // Reset lựa chọn ảnh mới.
                _selectedImageData = null;
                _selectedImageFileName = null;
                btnAddImageToLibrary.IsEnabled = false;
            }
            // Nếu chọn nhiều ảnh.
            else if (lvSavedImages.SelectedItems.Count > 1)
            {
                _selectedSavedImageId = null;
                txtImagePath.Text = $"Đã chọn {lvSavedImages.SelectedItems.Count} ảnh.";
            }
            // Nếu không chọn ảnh nào.
            else
            {
                _selectedSavedImageId = null;
                // Quay lại hiển thị ảnh đang được kích hoạt.
                await LoadCurrentSettingsAsync();
            }
        }

        // Xử lý nút "Chọn tất cả".
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            lvSavedImages.SelectAll();
        }

        // Xử lý nút "Bỏ chọn tất cả".
        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            lvSavedImages.UnselectAll();
        }

        // Xử lý nút "Xóa ảnh đã chọn".
        private async void BtnDeleteSelectedImages_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = lvSavedImages.SelectedItems.Cast<SavedImage>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một ảnh để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Bạn có chắc chắn muốn xóa {selectedItems.Count} ảnh đã chọn không? Hành động này không thể hoàn tác.",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            var idsToDelete = selectedItems.Select(item => item.ID).ToList();
            string idList = string.Join(",", idsToDelete);

            try
            {
                // Xóa các ảnh đã chọn khỏi CSDL.
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand($"DELETE FROM InterfaceImages WHERE ID IN ({idList})", connection);
                    command.CommandTimeout = 120;
                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    MessageBox.Show($"Đã xóa thành công {rowsAffected} ảnh.", "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadSavedImagesAsync(); // Tải lại thư viện.
                    await LoadCurrentSettingsAsync(); // Tải lại cài đặt để cập nhật ảnh active (nếu nó đã bị xóa).
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Hàm chuyển đổi mảng byte thành đối tượng BitmapImage.
        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));

            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze(); // Tối ưu hóa hiệu suất, cho phép truy cập từ các luồng khác.
            return image;
        }

        // Hàm lấy kiểu MIME của file ảnh.
        private string GetMimeType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                default:
                    return "application/octet-stream"; // Kiểu mặc định cho file nhị phân.
            }
        }
    }
}