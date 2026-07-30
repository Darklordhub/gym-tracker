using System.Net;
using System.Net.Http.Headers;
using backend.Services;

namespace backend.Tests;

public class ExerciseMediaUrlValidationServiceTests
{
    private static readonly IPAddress PublicAddress = IPAddress.Parse("93.184.216.34");

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://media.example/image.jpg")]
    [InlineData("gopher://media.example/image.jpg")]
    [InlineData("data:image/png;base64,AA==")]
    [InlineData("javascript:alert(1)")]
    public async Task ValidateImageUrl_RejectsUnsupportedSchemes(string url)
    {
        var handler = new StubHttpMessageHandler(_ => CreateImageResponse());
        var service = CreateService(handler, new StaticHostResolver(PublicAddress));

        var result = await service.ValidateImageUrlAsync(url);

        Assert.False(result.IsValid);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("http://localhost/image.jpg")]
    [InlineData("http://127.0.0.1/image.jpg")]
    [InlineData("http://10.1.2.3/image.jpg")]
    [InlineData("http://172.16.2.3/image.jpg")]
    [InlineData("http://192.168.2.3/image.jpg")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/image.jpg")]
    [InlineData("http://[fc00::1]/image.jpg")]
    [InlineData("http://[fe80::1]/image.jpg")]
    [InlineData("http://[::ffff:192.168.1.2]/image.jpg")]
    public async Task ValidateImageUrl_RejectsPrivateAndLocalTargets(string url)
    {
        var handler = new StubHttpMessageHandler(_ => CreateImageResponse());
        var service = CreateService(handler, new StaticHostResolver(PublicAddress));

        var result = await service.ValidateImageUrlAsync(url);

        Assert.False(result.IsValid);
        Assert.Equal("URL points to a blocked network location.", result.Error);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ValidateImageUrl_AcceptsSafePublicHttpsWithoutExternalNetwork()
    {
        var handler = new StubHttpMessageHandler(_ => CreateImageResponse());
        var service = CreateService(handler, new StaticHostResolver(PublicAddress));

        var result = await service.ValidateImageUrlAsync("https://media.example/exercises/squat.jpg");

        Assert.True(result.IsValid);
        Assert.True(result.CheckedRemotely);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Head, handler.LastMethod);
    }

    [Fact]
    public async Task ValidateImageUrl_RejectsRedirectToPrivateAddress()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data");
            return response;
        });
        var service = CreateService(handler, new StaticHostResolver(PublicAddress));

        var result = await service.ValidateImageUrlAsync("https://media.example/redirect.jpg");

        Assert.False(result.IsValid);
        Assert.Equal("URL points to a blocked network location.", result.Error);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ValidateImageUrl_ConnectionCallbackRejectsDnsRebinding()
    {
        var resolver = new SequenceHostResolver(
            [PublicAddress],
            [PublicAddress],
            [IPAddress.Loopback]);
        using var httpClient = new HttpClient(
            ExerciseMediaSafeHttpHandler.Create(resolver, TimeSpan.FromSeconds(2)))
        {
            Timeout = TimeSpan.FromSeconds(3),
        };
        var service = new ExerciseMediaUrlValidationService(httpClient, resolver);

        var result = await service.ValidateImageUrlAsync("http://rebind.example/image.jpg");

        Assert.False(result.IsValid);
        Assert.True(result.CheckedRemotely);
        Assert.True(resolver.CallCount >= 3);
        Assert.Equal("Unable to validate the URL against the remote server.", result.Error);
    }

    [Fact]
    public async Task ValidateImageUrl_GetFallbackRequestsOnlyTheFirstByte()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            Assert.Equal("bytes=0-0", request.Headers.Range?.ToString());
            return CreateImageResponse();
        });
        var service = CreateService(handler, new StaticHostResolver(PublicAddress));

        var result = await service.ValidateImageUrlAsync("https://media.example/exercises/squat.jpg");

        Assert.True(result.IsValid);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
    }

    private static ExerciseMediaUrlValidationService CreateService(
        HttpMessageHandler handler,
        IExerciseMediaHostResolver resolver)
    {
        return new ExerciseMediaUrlValidationService(
            new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(3),
            },
            resolver);
    }

    private static HttpResponseMessage CreateImageResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([]),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
    }

    private sealed class StaticHostResolver(params IPAddress[] addresses) : IExerciseMediaHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(addresses);
        }
    }

    private sealed class SequenceHostResolver(params IPAddress[][] results) : IExerciseMediaHostResolver
    {
        private readonly Queue<IPAddress[]> _results = new(results);
        private IPAddress[] _lastResult = results.Last();

        public int CallCount { get; private set; }

        public Task<IPAddress[]> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_results.Count > 0)
            {
                _lastResult = _results.Dequeue();
            }

            return Task.FromResult(_lastResult);
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastMethod = request.Method;
            return Task.FromResult(responseFactory(request));
        }
    }
}
