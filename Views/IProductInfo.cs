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