using System;
using Avalonia.Controls;
using VManager.Views;

namespace VManager.Services
{
    public static class ErrorService
    {
        private static ErrorWindow? _instance;
        private static bool _closed = true;

        public static void Show(Exception ex, Window? owner = null)
        {
            if (_instance == null || _closed)
            {
                _instance = new ErrorWindow(ex.Message);
                _closed = false;

                _instance.Closed += (_, _) =>
                {
                    _closed = true;
                    _instance = null;
                };

                if (owner != null)
                    _instance.Show(owner);
                else
                    _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }
    }
}