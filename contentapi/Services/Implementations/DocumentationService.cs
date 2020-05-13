//using System;
//using System.IO;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
//
//namespace contentapi.Services.Implementations
//{
//    public class DocumentationConfig
//    {
//        public string DocumentationFolder {get;set;}
//    }
//
//    public class DocumentationService
//    {
//        protected ILogger<DocumentationService> logger;
//        protected DocumentationConfig config;
//
//        public DocumentationService(ILogger<DocumentationService> logger, DocumentationConfig config)
//        {
//            this.logger = logger;
//            this.config = config;
//        }
//
//        public Task<string> GetDocumentation(string key)
//        {
//            var path = Path.Combine(config.DocumentationFolder, $"{key}.txt");
//
//            if(!File.Exists(path))
//                throw new InvalidOperationException($"No documentation for key {key}");
//
//            return File.ReadAllTextAsync(path);
//        }
//    }
//}