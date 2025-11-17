﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace namm
{
    // Lớp xử lý logic toàn cục cho ứng dụng, ví dụ như bắt các lỗi không mong muốn.
    public partial class App : Application
    {
        // Phương thức này được gọi khi ứng dụng bắt đầu khởi chạy.
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Đăng ký một hàm để xử lý các lỗi xảy ra trên luồng giao diện (UI thread).
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Đăng ký một hàm để xử lý các lỗi xảy ra trên các luồng nền (background threads).
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        // Hàm này sẽ được gọi khi có lỗi xảy ra trên luồng giao diện.
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Đánh dấu là lỗi đã được xử lý để ngăn ứng dụng bị sập ngay lập tức.
            e.Handled = true;
            ShowUnhandledException(e.Exception, "Lỗi Giao diện (UI Thread)");
        }

        // Hàm này sẽ được gọi khi có lỗi xảy ra trên một luồng nền.
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowUnhandledException(e.ExceptionObject as Exception, "Lỗi Luồng nền (Background Thread)");
        }

        // Hàm chung để hiển thị thông báo lỗi và đóng ứng dụng một cách an toàn.
        private void ShowUnhandledException(Exception? ex, string eventName)
        {
            string errorMessage;
            if (ex == null)
            {
                errorMessage = "Đã xảy ra một lỗi nghiêm trọng không xác định.";
            }
            else
            {
                // Tạo một thông báo lỗi chi tiết, bao gồm cả nguồn gốc và thông tin kỹ thuật của lỗi.
                errorMessage = $"Đã xảy ra một lỗi không thể phục hồi và ứng dụng sẽ thoát.\n\n" + $"Nguồn lỗi: {eventName}\n\n" + $"Chi tiết lỗi (vui lòng chụp lại màn hình này):\n\n" + $"{ex}";
            }

            // Hiển thị hộp thoại thông báo lỗi cho người dùng.
            MessageBox.Show(errorMessage, "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);

            // Sau khi người dùng nhấn OK, đóng ứng dụng một cách an toàn.
            Environment.Exit(1);
        }
    }
}
