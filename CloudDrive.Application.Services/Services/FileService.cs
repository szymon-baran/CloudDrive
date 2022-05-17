﻿using CloudDrive.Application.Abstraction;
using CloudDrive.Data.Abstraction;
using CloudDrive.Domain;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace CloudDrive.Application
{
    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDirectoryRepository _directoryRepository;
        private readonly IConfiguration _config;
        private readonly IDirectoryService _directoryService;

        public FileService(IFileRepository fileRepository, IUserRepository userRepository, IConfiguration config, IDirectoryRepository directoryRepository, IDirectoryService directoryService)
        {
            _fileRepository = fileRepository;
            _userRepository = userRepository;
            _config = config;
            _directoryRepository = directoryRepository;
            _directoryService = directoryService;
        }

        public async Task<List<FileDataDTO>> GetUserFiles(string username)
        {
            AppUser user = _userRepository.FirstOrDefault(x => x.Username == username) ?? throw new Exception("Nie znaleziono uzytkownika");
            return await _fileRepository.GetUserFiles(user.Id);
        }

        public async Task<UserFile> AddFile(AddUserFileVM file)
        {
            var fileUploadConfig = _config.GetSection("FileUploadConfig").Get<FileUploadConfig>();

            file.UserId = _userRepository.FirstOrDefault(x => x.Username == file.Username)?.Id;

            if (!file.DirectoryId.HasValue)
            {
                file.DirectoryId = _directoryRepository.FirstOrDefault(x => x.RelativePath == file.Username).Id;
            }

            UserDirectory userDirectory = _directoryRepository.FirstOrDefault(x => x.Id == file.DirectoryId);
            UserFile userFile = await _fileRepository.AddFile(file, userDirectory);

            using var stream = File.Create($"{fileUploadConfig.SaveFilePath}\\{userDirectory.RelativePath}\\{userFile.Id.ToString()}");
            await file.File.CopyToAsync(stream);

            return userFile;
        }

        public async Task<UserFile> AddFileByFileWatcher(AddUserFileVM file, string relativePath)
        {
            var fileUploadConfig = _config.GetSection("FileUploadConfig").Get<FileUploadConfig>();

            file.UserId = _userRepository.FirstOrDefault(x => x.Username == file.Username)?.Id;

            var relativePathWithFileName = relativePath;

            relativePath = file.Username + relativePath.TrimEnd('\\').Remove(relativePath.LastIndexOf('\\') + 1);   //usunięcie nazwy pliku ze ścieżki

            UserFile userFile = new();

            // w przypadku, gdy wrzucamy do głównego katalogu, który jest nadzorowany
            var mainDirectoryPath = file.Username + "\\";
            if (relativePath == mainDirectoryPath)
            {
                file.DirectoryId = _directoryRepository.FirstOrDefault(x => x.RelativePath == file.Username).Id;

                var mainDirectory = _directoryRepository.FirstOrDefault(x => x.Id == file.DirectoryId);
                userFile = await _fileRepository.AddFile(file, mainDirectory);

                using var stream = File.Create($"{fileUploadConfig.SaveFilePath}\\{mainDirectory.RelativePath}\\{userFile.Id.ToString()}");
                await file.File.CopyToAsync(stream);

                return userFile;
            }

            // pobranie katalogu do którego należy dodać plik
            var userDirectory = await _directoryRepository.FirstOrDefaultAsync(x => x.RelativePath == relativePath);

            // w przypadku, gdy katalogu nie ma na serwerze
            if (userDirectory == null)
            {
                var lastFolder = Path.GetDirectoryName(relativePath);
                var parentPath = Path.GetDirectoryName(lastFolder);

                UserDirectory parentDirectory = _directoryRepository.FirstOrDefault(x => x.RelativePath == parentPath);

                AddDirectoryVM addDirectoryVM = new()
                {
                    Name = relativePath.Split('\\').SkipLast(1).Last(), //wycięcie ze scieżki nazwy pliku
                    GeneratedPath = relativePath,
                    UserId = file.UserId,
                    ParentDirectoryId = parentDirectory.Id
                };

                await _directoryService.AddDirectory(addDirectoryVM, file.Username);
                userDirectory = _directoryRepository.FirstOrDefault(x => x.RelativePath == lastFolder);
            }

            userFile = await _fileRepository.AddFile(file, userDirectory);
            userFile.RelativePath = relativePathWithFileName;

            using var stream2 = File.Create($"{fileUploadConfig.SaveFilePath}\\{relativePath}{userFile.Id.ToString()}");
            await file.File.CopyToAsync(stream2);

            return userFile;
        }

        public async Task DeleteFile(string relativePath, string username)
        {
            relativePath = username + relativePath;
            var fileUploadConfig = _config.GetSection("FileUploadConfig").Get<FileUploadConfig>();

            var userId = _userRepository.FirstOrDefault(x => x.Username == username)?.Id;

            UserFile file = await _fileRepository.MarkFileAsDeleted(relativePath, userId);

            string oldPath = $"{fileUploadConfig.SaveFilePath}\\{relativePath.Replace(file.Name, file.Id.ToString())}";
            string newPath = $"{fileUploadConfig.SaveFilePath}\\{username}\\archive\\{file.Id}";

            File.Move(oldPath, newPath);
        }

        public async Task<DownloadFileDTO> DownloadFile(Guid fileId, string username)
        {
            var fileUploadConfig = _config.GetSection("FileUploadConfig").Get<FileUploadConfig>();
            var file = await _fileRepository.GetFileById(fileId);

            if (file == null)
            {
                return null;
            }

            string filePath = $"{fileUploadConfig.SaveFilePath}\\{username}\\{file.Id.ToString()}";

            var bytes = await File.ReadAllBytesAsync(filePath);

            return new DownloadFileDTO { Bytes = bytes, Path = filePath, UserFile = file };
        }
    }
}
