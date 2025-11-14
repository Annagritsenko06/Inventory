using CourseWork.Controllers;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text;

namespace CourseWork.Models
{
    public class SalesforceJwtAuth
    {
        private readonly IConfiguration _configuration;

        public SalesforceJwtAuth(IConfiguration config)
        {
            _configuration = config;
        }

        public async Task<(string accessToken, string instanceUrl)> GetAccessTokenAsync()
        {
            try
            {
                var sf = _configuration.GetSection("Salesforce");

                Console.WriteLine("Загружаем сертификат...");
                var certPath = Path.Combine(AppContext.BaseDirectory, "certs", "private_key.pfx");
                var cert = new X509Certificate2(certPath, sf["PrivateKeyPassword"], X509KeyStorageFlags.Exportable);

                

                Console.WriteLine("Формируем JWT...");
                var now = DateTime.UtcNow;

                var handler = new JwtSecurityTokenHandler();
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Issuer = sf["ClientId"],
                    Subject = new System.Security.Claims.ClaimsIdentity(new[] {
                        new System.Security.Claims.Claim("sub", sf["Username"])
                    }),
                    Audience = "https://login.salesforce.com",
                    Expires = now.AddMinutes(3),
                    SigningCredentials = new X509SigningCredentials(cert)
                };

                var jwt = handler.WriteToken(handler.CreateToken(tokenDescriptor));
                Console.WriteLine("JWT сформирован: " + jwt);

                using var client = new HttpClient();
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                    { "assertion", jwt }
                });

                Console.WriteLine("Отправляем запрос на Salesforce...");
                var response = await client.PostAsync(sf["LoginUrl"], content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Ошибка при получении токена: " + response.StatusCode);
                    Console.WriteLine("Ответ сервера: " + errorContent);
                    throw new HttpRequestException($"Ошибка при получении токена: {response.StatusCode}");
                }

                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var accessToken = json.RootElement.GetProperty("access_token").GetString();
                var instanceUrl = json.RootElement.GetProperty("instance_url").GetString();

                Console.WriteLine("Токен получен: " + accessToken);
                Console.WriteLine("Instance URL: " + instanceUrl);

                return (accessToken, instanceUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Исключение в GetAccessTokenAsync: " + ex.Message);
                throw;
            }
        }

        public async Task CreateAccountAndContactAsync(UserFormViewModel model)
        {
            try
            {
                Console.WriteLine("Получаем токен...");
                var (token, url) = await GetAccessTokenAsync();

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Account
                var accountPayload = new
                {
                    Name = model.CompanyName,
                    Phone = model.Phone,
                    BillingStreet = model.Address,
                    Rating = model.Rating,
                    AccountNumber = model.AccountNumber,
                    Website = model.Website,
                    Industry = model.Industry,
                    Employees = model.Employees,
                    AnnualRevenue = model.AnnualRevenue
                };

                Console.WriteLine("Создаём Account в Salesforce...");
                var accResp = await client.PostAsync(
                    $"{url}/services/data/v57.0/sobjects/Account",
                    new StringContent(JsonSerializer.Serialize(accountPayload), Encoding.UTF8, "application/json")
                );

                if (!accResp.IsSuccessStatusCode)
                {
                    var err = await accResp.Content.ReadAsStringAsync();
                    Console.WriteLine("Ошибка при создании Account: " + accResp.StatusCode);
                    Console.WriteLine("Ответ сервера: " + err);
                    throw new HttpRequestException($"Ошибка при создании Account: {accResp.StatusCode}");
                }

                var accId = JsonDocument.Parse(await accResp.Content.ReadAsStringAsync())
                                        .RootElement.GetProperty("id").GetString();
                Console.WriteLine("Account создан, ID: " + accId);

                // Contact
                var contactPayload = new
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Phone = model.Phone,
                    MailingStreet = model.Address,
                    Title = model.Title,
                    Department = model.Department,
                    Birthdate = model.Birthdate?.ToString("yyyy-MM-dd"), // Формат даты для Salesforce
                    AccountId = accId
                };

                Console.WriteLine("Создаём Contact в Salesforce...");
                var cResp = await client.PostAsync(
                    $"{url}/services/data/v57.0/sobjects/Contact",
                    new StringContent(JsonSerializer.Serialize(contactPayload), Encoding.UTF8, "application/json")
                );

                if (!cResp.IsSuccessStatusCode)
                {
                    var err = await cResp.Content.ReadAsStringAsync();
                    Console.WriteLine("Ошибка при создании Contact: " + cResp.StatusCode);
                    Console.WriteLine("Ответ сервера: " + err);
                    throw new HttpRequestException($"Ошибка при создании Contact: {cResp.StatusCode}");
                }

                Console.WriteLine("Contact создан успешно!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Исключение в CreateAccountAndContactAsync: " + ex.Message);
                throw;
            }
        }
    }
}
