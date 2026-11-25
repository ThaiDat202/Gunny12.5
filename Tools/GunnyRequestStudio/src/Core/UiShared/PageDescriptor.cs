using System;

namespace GunnyRequestStudio.Core.Ui
{
    /// <summary>
    /// Mo ta 1 trang de MainForm tao LAZY (chi tao khi nguoi dung mo lan dau).
    /// Giup app khoi dong nhanh, khong build ca 6 trang cung luc.
    /// </summary>
    public class PageDescriptor
    {
        public string Title;
        public string Icon;
        public string Subtitle;
        public Func<PageBase> Factory;

        public PageDescriptor(string title, string icon, Func<PageBase> factory, string subtitle = null)
        {
            Title = title;
            Icon = icon;
            Factory = factory;
            Subtitle = subtitle;
        }
    }
}
