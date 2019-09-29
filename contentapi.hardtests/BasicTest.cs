using System;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Threading.Tasks;

namespace contentapi.hardtests
{
    public class BasicTest : IClassFixture<WebApplicationFactory<contentapi.Startup>>
    {
        private readonly WebApplicationFactory<contentapi.Startup> contentApi;

        public BasicTest(WebApplicationFactory<contentapi.Startup> contentApi)
        {
            this.contentApi = contentApi;
        }

        [Theory]
        [InlineData("/api/users")]
        public void Test1(string url)
        {
            // Arrange
            var client = contentApi.CreateClient();

            // Act
            var response = client.GetAsync(url).Result;

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            var content = response.Content.ReadAsStringAsync().Result;
            Assert.True(content.Length > 0);
            //Assert.Equal("application/json; charset=utf-8",
            //    response.Content.Headers.ContentType.ToString());
        }
    }
}
