using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CommonTools
{
    public class FTPS
    {
        //Uri ftp path, eg ftp://server/img/
        private readonly string ftpPath;

        //Creds to ftp auth
        private readonly NetworkCredential ftpAuth;

        //Maximum file size
        private readonly int fileSize;

        //Number of parallel uploads
        private readonly SemaphoreSlim slimSem;

        public FTPS(string path, string user, string pass, int limit = 10, int size = 5000000)
        {
            ftpAuth = new NetworkCredential(user, pass);
            ftpPath = path;
            fileSize = size;
            slimSem = new SemaphoreSlim(limit);

            //Always accepting servers sertificate
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        /// <summary>
        /// Use to upload multiple files to ftp folder. Pass array with source file paths.
        /// </summary>
        public void Upload(string path, string[] files)
        {
            try
            {
                if (CheckAndCreateForder(path))
                {
                    foreach (var file in files)
                    {
                        UploadOne(path, Path.GetFileName(file), File.ReadAllBytes(file));
                    }
                }
            }
            catch
            {
                //Catch to log
            }
        }

        /// <summary>
        /// Use to upload one file to ftp folder. Pass one file path.
        /// </summary>
        public void Upload(string path, string fpath)
        {
            Upload(path, Path.GetFileName(fpath), File.ReadAllBytes(fpath));
        }

        /// <summary>
        /// Use to upload one file to ftp folder. Pass one file name and byte array content
        /// </summary>
        public void Upload(string path, string fname, byte[] content)
        {
            try
            {
                if (CheckAndCreateForder(path))
                    UploadOne(path, fname, content);
            }
            catch
            {
                //Catch to log
            }
        }

        private void UploadOne(string path, string fname, byte[] content)
        {
            if (IsValidImageFile(content))
            {
                var fullPath = $"{ftpPath}/{path}/{fname}";
                var uploadReq = CreateFtpReq(fullPath, WebRequestMethods.Ftp.UploadFile);

                Task.Run(() =>
                {
                    slimSem.Wait();

                    Console.WriteLine($"Start uploading [{fname}]");
                    uploadReq.ContentLength = content.Length;

                    using Stream requestStream = uploadReq.GetRequestStream();
                    requestStream.Write(content, 0, content.Length);

                }).ContinueWith(x =>
                {
                    using (FtpWebResponse response = (FtpWebResponse)uploadReq.GetResponse())
                    {
                        if (response.StatusCode == FtpStatusCode.ClosingData)
                            Console.WriteLine($"[{fname}] uploaded!");
                    }

                    slimSem.Release();
                });
            }
            else
                Console.WriteLine($"[{fname}] not valid, skipping...");
        }

        private FtpWebRequest CreateFtpReq(string path, string method)
        {
            FtpWebRequest req = (FtpWebRequest)WebRequest.Create(path);
            req.Method = method;
            req.Credentials = ftpAuth;
            req.UseBinary = true;
            req.UsePassive = true;
            req.KeepAlive = true;
            req.EnableSsl = true;

            return req;
        }

        private bool CheckAndCreateForder(string path)
        {
            try
            {
                //Another way - generate webException with ListDirectory on path
                //We just searching path in basePath
                var checkReq = CreateFtpReq(ftpPath, WebRequestMethods.Ftp.ListDirectory);

                using (FtpWebResponse response = (FtpWebResponse)checkReq.GetResponse())
                {
                    using StreamReader stream = new StreamReader(response.GetResponseStream());
                    while (!stream.EndOfStream)
                    {
                        //If path exist in directory
                        if (stream.ReadLine() == path)
                        {
                            Console.WriteLine($"Path [{path}] found!");
                            return true;
                        }
                    }
                }

                //Folder not found > create new
                var createReq = CreateFtpReq($"{ftpPath}/{path}", WebRequestMethods.Ftp.MakeDirectory);

                using (FtpWebResponse response = (FtpWebResponse)createReq.GetResponse())
                {
                    Console.WriteLine($"Path [{path}] created!");
                    return response.StatusCode == FtpStatusCode.PathnameCreated;
                }
            }
            catch
            {
                //Catch to log
                return false;
            }
        }


        private bool IsValidImageFile(byte[] input)
        {
            byte[] buffer = new byte[8];

            var gif87a = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 };
            var gif89a = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
            var png = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF };

            try
            {
                if (input.Length <= fileSize)
                {
                    Array.Copy(input, buffer, 8);

                    return (ByteArrayStartsWith(buffer, gif87a) ||
                      ByteArrayStartsWith(buffer, gif89a) ||
                      ByteArrayStartsWith(buffer, png) ||
                      ByteArrayStartsWith(buffer, jpeg));
                }
            }
            catch
            {
                //Catch to log
            }

            return false;
        }

        private bool ByteArrayStartsWith(byte[] a, byte[] b)
        {
            if (a.Length >= b.Length)
            {
                for (int i = 0; i < b.Length; i++)
                {
                    if (a[i] != b[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
