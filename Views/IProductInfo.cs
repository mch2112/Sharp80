/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

namespace Sharp80.Views
{
    public interface IProductInfo
    {
        string DownloadURL { get; }
        string ProductAuthor { get; }
        string ProductName { get; }
        string ProductURL { get; }
        string ProductVersion { get; }
    }
}