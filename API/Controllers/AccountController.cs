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
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class AccountController : BaseApiController
    {
        private readonly DataContext _context;
        public ITokenService _tokenService { get; }
        private readonly IMapper _mapper;
        public AccountController(DataContext context, ITokenService tokenService, IMapper mapper)
        {
            _mapper = mapper;
            _tokenService = tokenService;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register( RegisterDto registerDto )
        {
            if( await ValidUsername(registerDto.Username)) return BadRequest("Username already taken");
            
            var user = _mapper.Map<AppUser>(registerDto);
            
            using var hmac = new HMACSHA512();

            user.UserName = registerDto.Username.ToLower();
            user.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password));
            user.PasswordSalt = hmac.Key;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new UserDto()
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user),
                KnownAs = user.KnownAs
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
                PhotoUrl = user.Photos.FirstOrDefault(x => x.IsMain)?.Url,
                KnownAs = user.KnownAs
            };

        }

        private async Task<bool> ValidUsername(string username) 
        {
            return await _context.Users.AnyAsync( x => x.UserName == username.ToLower());
        }

    }
}