using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class AccountController : BaseApiController
    {
        private readonly DataContext _context;
        public ITokenService _tokenService { get; }
        public AccountController( DataContext context, ITokenService tokenService)
        {
            _tokenService = tokenService;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register( RegisterDto register )
        {
            if( await ValidUsername(register.Username)) return BadRequest("Username already taken");

            using var hmac = new HMACSHA512();

            var user = new AppUser
            {
                UserName = register.Username,
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(register.Password)),
                PasswordSalt = hmac.Key
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new UserDto()
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user)
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login( LoginDto login )
        {
            AppUser user = await _context.Users
                                .Include(p => p.Photos)
                                .SingleOrDefaultAsync( x => x.UserName == login.Username );
            if( user == null ) return BadRequest("Invalid Username");

            var hmac = new HMACSHA512( user.PasswordSalt );

            var computedhash = hmac.ComputeHash( Encoding.UTF8.GetBytes(login.Password));
            
            for( int i = 0; i<computedhash.Length; i++ )
            {
                if( computedhash[i] != user.PasswordHash[i] ) return BadRequest("Invalid Password");
            }

            return new UserDto()
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user),
                PhotoUrl = user.Photos.FirstOrDefault(x => x.IsMain)?.Url
            };

        }

        private async Task<bool> ValidUsername(string username) 
        {
            return await _context.Users.AnyAsync( x => x.UserName == username.ToLower());
        }

    }
}