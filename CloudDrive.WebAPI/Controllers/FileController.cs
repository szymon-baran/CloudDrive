﻿using CloudDrive.Application;
using CloudDrive.Domain;
using CloudDrive.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Reflection.Metadata;
using System.Security.Claims;

namespace CloudDrive.WebAPI
{
    public class FileController : AppController
    {
        private readonly IFileService _fileService;
        private readonly IDirectoryService _directoryService;
        private readonly IHubContext<FileHub, IFileHub> _hubContext;

        public FileController(IFileService fileService, IDirectoryService directoryService, IHubContext<FileHub, IFileHub> hubContext)
        {
            _fileService = fileService;
            _directoryService = directoryService;
            _hubContext = hubContext;
        }

        [Authorize]
        [HttpPost("uploadFile")]
        public async Task<IActionResult> UploadFile()
        {
            var files = Request.Form.Files;
            var loggedUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (loggedUsername == null)
            {
                return NotFound("Błąd przy próbie znalezienia użytkownika");
            }

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    AddUserFileVM userFile = new()
                    {
                        File = file,
                        Username = loggedUsername,
                    };

                    UserFile addedFile = await _fileService.AddFile(userFile);
                    await _hubContext.Clients.All.FileAdded(addedFile.Id, addedFile.Name);
                }
            }
            
            return Ok();
        }

        [Authorize]
        [HttpDelete("deleteFile")]
        public async Task<IActionResult> DeleteFile(string relativePath)
        {
            var loggedUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (loggedUsername == null)
            {
                return NotFound("Błąd przy próbie znalezienia użytkownika");
            }

            await _fileService.DeleteFile(relativePath, loggedUsername);

            return Ok();
        }
        
        [Authorize]
        [HttpPost("addDirectory")]
        public async Task<IActionResult> AddDirectory(AddDirectoryVM model)
        {
            var loggedUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (loggedUsername == null)
            {
                return NotFound("Błąd przy próbie znalezienia użytkownika");
            }

            await _directoryService.AddDirectory(model, loggedUsername);

            return Ok();
        }
        
        [Authorize]
        [HttpGet("getDirectoriesToSelectList")]
        public async Task<IActionResult> GetDirectoriesToSelectList()
        {
            var loggedUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (loggedUsername == null)
            {
                return NotFound("Błąd przy próbie znalezienia użytkownika");
            }

            List<DirectorySelectBoxVM> list = await _directoryService.GetDirectoriesToSelectList(loggedUsername);

            return Ok(list);
        }

        [Authorize]
        [HttpGet("downloadFile")]
        public async Task<IActionResult> DownloadFile(Guid fileId)
        {
            var loggedUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (loggedUsername == null)
            {
                return NotFound("Błąd przy próbie znalezienia użytkownika");
            }

            DownloadFileDTO downloadedFile = await _fileService.DownloadFile(fileId, loggedUsername);

            if (downloadedFile == null)
            {
                return NotFound("Brak pliku do pobrania");
            }

            return File(downloadedFile.Bytes, downloadedFile.UserFile.ContentType, downloadedFile.UserFile.Name);
        }
    }
}