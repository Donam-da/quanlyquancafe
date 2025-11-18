﻿﻿// File này chứa logic cho cửa sổ (dialog) lựa chọn kiểu và số lượng đồ uống.
// Chức năng chính:
// 1. Tự động tạo giao diện nhập liệu dựa trên các "kiểu" đồ uống có sẵn (ví dụ: "Pha chế", "Nguyên bản").
// 2. Hiển thị số lượng tồn kho cho mỗi kiểu.
// 3. Kiểm tra tính hợp lệ của số lượng người dùng nhập (phải là số, không âm, không vượt tồn kho).
// 4. Trả về số lượng đã chọn cho mỗi kiểu về màn hình gọi nó.
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho cửa sổ SelectDrinkTypeDialog.
    public partial class SelectDrinkTypeDialog : Window
    {
        // Dictionary để lưu trữ các control được tạo động.
        // Key: Tên kiểu (ví dụ: "Nguyên bản").
        // Value: Một Tuple chứa (TextBox nhập số lượng, số lượng tồn kho).
        private readonly Dictionary<string, (TextBox textBox, int stock)> _typeControls = new Dictionary<string, (TextBox, int)>();
        // Dictionary để lưu kết quả người dùng chọn. Sẽ được đọc bởi cửa sổ gọi dialog này.
        public Dictionary<string, int> SelectedQuantities { get; } = new Dictionary<string, int>();

        // Hàm khởi tạo của cửa sổ, nhận tên đồ uống và danh sách các kiểu có sẵn cùng tồn kho.
        public SelectDrinkTypeDialog(string drinkName, Dictionary<string, int> availableStock)
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
            tbDrinkName.Text = drinkName; // Hiển thị tên đồ uống lên tiêu đề của dialog.

            int rowIndex = 0;
            // Duyệt qua danh sách các kiểu đồ uống có sẵn để tự động tạo giao diện.
            foreach (var typeStockPair in availableStock)
            {
                string typeName = typeStockPair.Key;
                int stock = typeStockPair.Value;

                // Thêm một dòng mới vào Grid để chứa các control cho kiểu này.
                gridDrinkTypes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Tạo nhãn hiển thị tên kiểu (ví dụ: "Pha chế:").
                var typeLabel = new TextBlock
                {
                    Text = $"{typeName}:",
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5)
                };
                Grid.SetRow(typeLabel, rowIndex); // Đặt nhãn vào dòng hiện tại.
                Grid.SetColumn(typeLabel, 0); // Đặt nhãn vào cột 0.

                // Tạo một StackPanel để nhóm ô nhập liệu và nhãn tồn kho lại với nhau.
                var inputPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(inputPanel, rowIndex);
                Grid.SetColumn(inputPanel, 1);

                // Tạo ô TextBox để người dùng nhập số lượng.
                var textBox = new TextBox
                {
                    Name = "txt" + typeName.Replace(" ", ""),
                    Text = "", // Ban đầu để trống.
                    Width = 50,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                // Tạo nhãn hiển thị số lượng tồn kho (ví dụ: "/ 10").
                var stockLabel = new TextBlock { Text = $"/ {stock}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0), Foreground = Brushes.Gray };

                inputPanel.Children.Add(textBox); // Thêm TextBox vào panel.
                inputPanel.Children.Add(stockLabel); // Thêm nhãn tồn kho vào panel.

                gridDrinkTypes.Children.Add(typeLabel); // Thêm nhãn tên kiểu vào Grid chính.
                gridDrinkTypes.Children.Add(inputPanel); // Thêm panel nhập liệu vào Grid chính.

                // Lưu lại TextBox và số lượng tồn kho vào Dictionary để kiểm tra sau này.
                _typeControls.Add(typeName, (textBox, stock));

                rowIndex++;
            }
        }

        // Xử lý sự kiện khi người dùng nhấn nút "Đồng ý".
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            bool hasError = false;
            bool hasValue = false;

            // Duyệt qua các control đã được tạo để lấy và kiểm tra dữ liệu.
            foreach (var pair in _typeControls)
            {
                string type = pair.Key;
                TextBox textBox = pair.Value.textBox;
                int stock = pair.Value.stock;

                // Nếu ô nhập trống, coi như số lượng là 0 và bỏ qua, không xử lý.
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    continue;
                }

                if (int.TryParse(textBox.Text, out int quantity))
                {
                    // Kiểm tra số lượng không được là số âm.
                    if (quantity < 0)
                    {
                        MessageBox.Show($"Số lượng cho kiểu '{type}' không thể là số âm.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        hasError = true;
                        break;
                    }
                    // Kiểm tra số lượng không được vượt quá tồn kho.
                    if (quantity > stock)
                    {
                        MessageBox.Show($"Số lượng cho kiểu '{type}' không được vượt quá số lượng tồn kho ({stock}).", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        hasError = true;
                        break;
                    }
                    // Nếu số lượng hợp lệ và lớn hơn 0.
                    if (quantity > 0)
                    {
                        SelectedQuantities[type] = quantity; // Thêm vào danh sách kết quả.
                        hasValue = true; // Đánh dấu là đã có ít nhất một giá trị được nhập.
                    }
                }
                else
                {
                    // Nếu người dùng nhập không phải là số.
                    MessageBox.Show($"Số lượng cho kiểu '{type}' không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    hasError = true;
                    break;
                }
            }

            // Nếu có lỗi trong quá trình kiểm tra, dừng lại không đóng cửa sổ.
            if (hasError) return;

            // Nếu người dùng không nhập bất kỳ số lượng nào.
            if (!hasValue)
            {
                MessageBox.Show("Vui lòng nhập số lượng lớn hơn 0 cho ít nhất một kiểu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Nếu mọi thứ hợp lệ, đặt DialogResult là true để báo hiệu thành công và đóng cửa sổ.
            this.DialogResult = true;
        }

        // Sự kiện được gọi khi cửa sổ được tải xong.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Tự động đặt con trỏ vào ô nhập liệu đầu tiên để người dùng có thể nhập ngay.
            _typeControls.Values.FirstOrDefault().textBox?.Focus();
        }
    }
}