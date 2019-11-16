using CommonTools;
using System;
using System.IO;

namespace Img2FTPS_Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            new FTPS("ftp://server/img/", "login", "password").Upload("test", Directory.GetFiles(@"D:\test\"));
            Console.ReadKey();
        }
    }
}
