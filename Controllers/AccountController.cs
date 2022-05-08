using Blog.Data;
using Blog.Extensions;
using Blog.Models;
using Blog.Services;
using Blog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureIdentity.Password;

namespace Blog.Controllers
{
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly TokenService _tokenService;

        public AccountController(TokenService tokenService)
        {
            _tokenService = tokenService;
        }


        [HttpPost("v1/accounts")]
        public async IActionResult Post(
            [FromBody]RegisterViewModel model,
            [FromServices]BlogDataContext context)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ResultViewModel<string>(ModelState.GetErrors()));

            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                Slug = model.Email.Replace("@", "-").Replace(".", "-")
            };

            var password = PasswordGenerator.Generate();
            user.PasswordHash = PasswordHasher.Hash(password);

            try
            {
                await context.Users.AddAsync(user);
                await context.SaveChangesAsync();

                return Ok(new ResultViewModel<dynamic>(new
                {
                    user = user.Email
                }));
            } catch (DbUpdateException e)
            {
                return StatusCode(400, new ResultViewModel<string>("Email já cadastrado"));
            } catch (Exception e)
            {
                return StatusCode(500, new ResultViewModel<string>("Falha interna do servidor"));
            }
        }

        [HttpPost("v1/accounts/login")]
        public async Task<IActionResult> Login(
            [FromBody]LoginViewModel model,
            [FromServices]BlogDataContext context)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new ResultViewModel<string>(ModelState.GetErrors()));

                var user = await context.Users
                    .AsNoTracking()
                    .Include(u => u.Roles)
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user == null)
                    return StatusCode(401, new ResultViewModel<string>("Usuário ou senha inválidos"));

                bool isPasswordValid = PasswordHasher.Verify(user.PasswordHash, model.Password);
                if (!isPasswordValid)
                    return StatusCode(401, new ResultViewModel<string>("Usuário ou senha inválidos"));

                var token = _tokenService.GenerateToken(user);
                return Ok(new ResultViewModel<string>(token, null));
            }
            catch (Exception e)
            {
                return StatusCode(500, new ResultViewModel<string>("Falha interna do servidor"));
            }
        }
    }
}
