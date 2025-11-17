﻿﻿﻿using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace namm
{
    // Lớp này đại diện cho một món hàng trong hóa đơn (bill).
    // Nó triển khai INotifyPropertyChanged để giao diện có thể tự động cập nhật khi dữ liệu thay đổi.
    public class BillItem : INotifyPropertyChanged
    {
        // ID của đồ uống trong cơ sở dữ liệu.
        public int DrinkId { get; set; }
        // Tên hiển thị của đồ uống.
        public string DrinkName { get; set; } = string.Empty;
        // Mã định danh cho loại đồ uống (ví dụ: caphesua_NB cho cà phê sữa nguyên bản).
        public string DrinkTypeCode { get; set; } = string.Empty;
        // Loại đồ uống, ví dụ: "Nguyên bản" hoặc "Pha chế".
        public string DrinkType { get; set; } = string.Empty;

        // Biến riêng tư để lưu trữ số lượng.
        private int _quantity;
        // Thuộc tính công khai cho số lượng.
        public int Quantity
        {
            get => _quantity;
            set
            {
                // Chỉ cập nhật và thông báo nếu giá trị thực sự thay đổi.
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(); // Thông báo cho UI rằng thuộc tính 'Quantity' đã thay đổi.
                    OnPropertyChanged(nameof(TotalPrice)); // Thông báo cho UI rằng 'TotalPrice' cũng thay đổi theo.
                }
            }
        }

        // Giá của một đơn vị đồ uống.
        public decimal Price { get; set; }
        // Tổng tiền cho món hàng này (tự động tính toán).
        public decimal TotalPrice => Quantity * Price;

        // Sự kiện được kích hoạt khi một thuộc tính thay đổi, cần thiết cho INotifyPropertyChanged.
        public event PropertyChangedEventHandler? PropertyChanged;

        // Phương thức trợ giúp để kích hoạt sự kiện PropertyChanged.
        // [CallerMemberName] tự động lấy tên của thuộc tính đã gọi phương thức này.
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}