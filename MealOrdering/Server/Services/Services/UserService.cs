﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using MealOrdering.Server.Data.Context;
using MealOrdering.Server.Services.Infrastruce;
using MealOrdering.Shared.DTO;
using MealOrdering.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace MealOrdering.Server.Services.Services
{
    public class UserService : IUserService
    {
        private readonly IMapper mapper;
        private readonly MealOrderingDbContext context;
        private readonly IConfiguration configuration;

        public UserService(IMapper Mapper, MealOrderingDbContext Context, IConfiguration Configuration)
        {
            mapper = Mapper;
            context = Context;
            configuration = Configuration;
        }


        public async Task<UserDTO> CreateUser(UserDTO User)
        {
            var dbUser = await context.Users.Where(i => i.Id == User.Id).FirstOrDefaultAsync();

            if (dbUser != null)
                throw new Exception("İlgili Kayıt Zaten Mevcut");


            dbUser = mapper.Map<Data.Models.Users>(User);

            await context.Users.AddAsync(dbUser);
            int result = await context.SaveChangesAsync();

            return mapper.Map<UserDTO>(dbUser);
        }

        public async Task<bool> DeleteUserById(Guid Id)
        {
            var dbUser = await context.Users.Where(i => i.Id == Id).FirstOrDefaultAsync();

            if (dbUser == null)
                throw new Exception("Kullanıcı Bulunamadı");

            context.Users.Remove(dbUser);
            int result = await context.SaveChangesAsync();

            return result > 0;
        }

        public async Task<UserDTO> GetUserById(Guid Id)
        {
            return await context.Users
                        .Where(i => i.Id == Id)
                        .ProjectTo<UserDTO>(mapper.ConfigurationProvider)
                        .FirstOrDefaultAsync();
        }

        public async Task<List<UserDTO>> GetUsers()
        {
            return await context.Users
                        .Where(i => i.IsActive)
                        .ProjectTo<UserDTO>(mapper.ConfigurationProvider)
                        .ToListAsync();
        }

        public async Task<String> Login(string EMail, string Password)
        {
            // Veritabanı Kullanıcı Doğrulama İşlemleri Yapıldı.

            var encryptedPassword = PasswordEncrypter.Encrypt(Password);

            var dbUser = await context.Users.FirstOrDefaultAsync(i => i.EMailAddress == EMail && i.Password == encryptedPassword);

            if (dbUser == null)
                throw new Exception("Kullanıcı Bulunamadı veya Bilgiler Yanlış");

            if (!dbUser.IsActive)
                throw new Exception("Kullanıcı Pasif Durumdadır!");


            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtSecurityKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.Now.AddDays(int.Parse(configuration["JwtExpiryInDays"].ToString()));

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, EMail),
                new Claim(ClaimTypes.Name, dbUser.FirstName + " " + dbUser.LastName)
            };

            var token = new JwtSecurityToken(configuration["JwtIssuer"], configuration["JwtAudience"], claims, null, expiry, creds);

            String tokenStr = new JwtSecurityTokenHandler().WriteToken(token);

            return tokenStr;
        }

        public async Task<UserDTO> UpdateUser(UserDTO User)
        {
            var dbUser = await context.Users.Where(i => i.Id == User.Id).FirstOrDefaultAsync();

            if (dbUser == null)
                throw new Exception("İlgili Kayıt Bulunamadı");


            mapper.Map(User, dbUser);

            int result = await context.SaveChangesAsync();

            return mapper.Map<UserDTO>(dbUser);
        }
    }
}
