/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details. 

using System;
using Sharp80.Views;

namespace Sharp80
{
    public class ProductInfo : IProductInfo
    {
        public string ProductName => "Sharp 80";
        public string ProductAuthor => "Matthew Hamilton";
        public string ProductURL => "http://www.sharp80.com";
        public string DownloadURL => ProductName + "/download.php";
        public string ProductVersion => System.Windows.Forms.Application.ProductVersion;
    }
}
