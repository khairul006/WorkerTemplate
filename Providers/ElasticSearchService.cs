using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WorkerTemplate.Configs;

namespace WorkerTemplate.Providers
{
    public class ElasticSearchService
    {
        private readonly ILogger<ElasticSearchService> _logger;
        private readonly ElasticSearchSettings _settings;
        private readonly HttpClient _httpClient;

        public ElasticSearchService(
            IOptions<ElasticSearchSettings> options,
            ILogger<ElasticSearchService> logger,
            HttpClient httpClient
        )
        {
            _settings = options.Value;
            _logger = logger;
            _httpClient = httpClient;

            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}"))
            );
        }

        public async Task<(object data, int status)> IndexDocumentAsync(
            string id,
            object document,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_settings.BaseUrl}/{_settings.Index}/_doc/{id}";

                var payload = JsonSerializer.Serialize(document);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(url, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<object>(body);

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Elasticsearch returned {(int)response.StatusCode}: {body}");

                return (data!, (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Elasticsearch request aborted due to cancellationToken.");
                throw;
            }
            catch (TaskCanceledException)
            {
                throw new Exception($"Elasticsearch request timed out. URL: {_settings.BaseUrl}/{_settings.Index}/_doc/{id}");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Elasticsearch connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Elasticsearch error: {ex.Message}");
            }
        }


        public async Task<(object data, int status)> UpdateDocumentAsync(
            string id,
            object updateBody,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_settings.BaseUrl}/{_settings.Index}/_update/{id}";
                var payload = JsonSerializer.Serialize(updateBody);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<object>(body);

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Elasticsearch returned {(int)response.StatusCode}: {body}");

                return (data!, (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Elasticsearch request aborted due to cancellationToken.");
                throw;
            }
            catch (TaskCanceledException)
            {
                throw new Exception($"Elasticsearch request timed out. URL: {_settings.BaseUrl}/{_settings.Index}/_update/{id}");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Elasticsearch connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Elasticsearch error: {ex.Message}");
            }
        }
    }
}
