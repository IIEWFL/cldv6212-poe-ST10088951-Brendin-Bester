using Azure.Storage.Files.Shares;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Services
{
    public class FileShareStorageService
    {
        private readonly ShareClient _shareClient;

        public FileShareStorageService(string storageConnectionString, string shareName)
        {
            var serviceClient = new ShareServiceClient(storageConnectionString);
            _shareClient = serviceClient.GetShareClient(shareName);
            _shareClient.CreateIfNotExists();
        }

        //upload log to fileshare

        public async Task UploadFileAsync(string fileName, Stream fileStream)
        {
            var directoryClient = _shareClient.GetRootDirectoryClient();
            var fileClient = directoryClient.GetFileClient(fileName);

            await fileClient.CreateAsync(fileStream.Length);

            //handeling
            long position = 0;
            int buffersize = 4 * 1024;
            byte[] buffer = new byte[buffersize];
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffersize)) > 0)
            {
                using (var memoryStream = new MemoryStream(buffer, 0, bytesRead))
                {
                    await fileClient.UploadRangeAsync(
                        Azure.Storage.Files.Shares.Models.ShareFileRangeWriteType.Update,
                        new Azure.HttpRange(position, bytesRead), memoryStream);
                }
                position += bytesRead;
            }
        }
    }
}
