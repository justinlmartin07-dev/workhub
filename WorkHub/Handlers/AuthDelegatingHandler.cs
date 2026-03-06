using System.Net;
using System.Net.Http.Headers;

namespace WorkHub.Handlers;

public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly Services.AuthService _authService;

    public AuthDelegatingHandler(Services.AuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authService.GetValidTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.LogoutAsync();
            Application.Current?.Dispatcher.Dispatch(async () =>
            {
                if (Application.Current?.MainPage != null)
                {
                    await Shell.Current.GoToAsync("../login");
                }
            });
        }

        return response;
    }
}