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
        private readonly IHubContext<FileHub, IFileHub> _hubContext;

        public FileController(IFileService fileService, IHubContext<FileHub, IFileHub> hubContext)
        {
            _fileService = fileService;
            _hubContext = hubContext;
        }

        [Authorize]
        [HttpPost("uploadFile")]
        public async Task<IActionResult> UploadFile()
        {
            var file = Request.Form.Files.FirstOrDefault();
            var loggedUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (loggedUsername == null)
            {
                return NotFound("Błąd przy próbie znalezienia użytkownika");
            }

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

            return Ok();
        }
    }
}