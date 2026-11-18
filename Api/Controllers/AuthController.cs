using CampusFlow.Application.Services;
using CampusFlow.Contracts.Auth;
using CampusFlow.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusFlow.Api.Controllers;

/// <summary>Вход в систему и данные текущего пользователя.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Вход по логину и паролю. В MVP пароль совпадает с логином.</summary>
    /// <response code="200">Токен и данные пользователя.</response>
    /// <response code="400">Не переданы login или password.</response>
    /// <response code="401">Неверный логин или пароль.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest? request,
        CancellationToken cancellationToken)
    {
        return Ok(await _auth.LoginAsync(request, cancellationToken));
    }

    /// <summary>Данные пользователя, которому принадлежит токен (удобно для фронтенда).</summary>
    /// <response code="200">Пользователь из токена.</response>
    /// <response code="401">Токен отсутствует или неверен.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        return Ok(await _auth.GetCurrentUserAsync(cancellationToken));
    }
}