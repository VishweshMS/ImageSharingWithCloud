using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageSharingWithCloud.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.EntityFrameworkCore;

namespace ImageSharingWithCloud.DAL
{
    public class ImageStorage : IImageStorage
    {
        protected ILogger<ImageStorage> logger;

        protected CosmosClient imageDbClient;

        protected string imageDatabase;

        protected Container imageDbContainer;

        protected BlobContainerClient blobContainerClient;


        public ImageStorage(IConfiguration configuration,
                            CosmosClient imageDbClient,
                            ILogger<ImageStorage> logger)
        {
            this.logger = logger;

            /*
             * Use Cosmos DB client to store metadata for images.
             */
            this.imageDbClient = imageDbClient;

            this.imageDatabase = configuration[StorageConfig.ImageDbDatabase];
            string imageContainer = configuration[StorageConfig.ImageDbContainer];
            logger.LogInformation("ImageDb (Cosmos DB) is being accessed here: " + imageDbClient.Endpoint);
            logger.LogDebug("ImageDb using database {0} and container {1}",
                imageDatabase, imageContainer);
            Database imageDbDatabase = imageDbClient.GetDatabase(imageDatabase);
            this.imageDbContainer = imageDbDatabase.GetContainer(imageContainer);

            /*
             * Use Blob storage client to store images in the cloud.
             */
            string imageStorageUriFromConfig = configuration[StorageConfig.ImageStorageUri];
            if (imageStorageUriFromConfig == null)
            {
                throw new ArgumentNullException("Missing Blob service URI in configuration: " + StorageConfig.ImageStorageUri);
            }
            Uri imageStorageUri = new Uri(imageStorageUriFromConfig);

            // TODO get the shared key credential for accessing blob storage.
           // StorageSharedKeyCredential credential = null;
            
            //BlobServiceClient blobServiceClient = new BlobServiceClient(imageStorageUri, credential, null);
            // Retrieve account name and key from the configuration.
            string accountName = configuration[StorageConfig.ImageStorageAccountName];
            string accountKey = configuration[StorageConfig.ImageStorageAccessKey];

            if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
            {
                throw new ArgumentNullException("Missing Blob storage account name or key in configuration.");
            }

            // Initialize the credential with the account name and account key.
            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(accountName, accountKey);

            // Now, create the BlobServiceClient with the endpoint and the credential.
            BlobServiceClient blobServiceClient = new BlobServiceClient(imageStorageUri, credential);

            // ... rest of the constructor code.


            string storageContainer = configuration[StorageConfig.ImageStorageContainer];
            if (storageContainer == null)
            {
                throw new ArgumentNullException("Missing Blob container name in configuration: " + StorageConfig.ImageStorageContainer);
            }
            this.blobContainerClient = blobServiceClient.GetBlobContainerClient(storageContainer);
            logger.LogInformation("ImageStorage (Blob storage) being accessed here: " + blobContainerClient.Uri);
        }

        /**
         * Use this to generate the singleton Cosmos DB client that is injected into all instances of ImageStorage.
         */
        public static CosmosClient GetImageDbClient(IWebHostEnvironment environment, IConfiguration configuration)
        {
            string imageDbUri = configuration[StorageConfig.ImageDbUri];
            if (imageDbUri == null)
            {
                throw new ArgumentNullException("Missing configuration: " + StorageConfig.ImageDbUri);
            }
            string imageDbAccessKey = configuration[StorageConfig.ImageDbAccessKey];

            CosmosClientOptions cosmosClientOptions = null;
            //if (environment.IsDevelopment())
            //{
            //    cosmosClientOptions = new CosmosClientOptions()
            //    {
            //        HttpClientFactory = () =>
            //        {
            //            HttpMessageHandler httpMessageHandler = new HttpClientHandler()
            //            {
            //                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            //            };

            //            return new HttpClient(httpMessageHandler);
            //        },
            //        ConnectionMode = ConnectionMode.Gateway
            //    };
            //}

            CosmosClient imageDbClient = new CosmosClient(imageDbUri, imageDbAccessKey, cosmosClientOptions);
            return imageDbClient;
        }

        /*
         * 
         */
        public async Task InitImageStorage()
        {

            logger.LogInformation("Initializing image storage (Cosmos DB)....");
            await imageDbClient.CreateDatabaseIfNotExistsAsync(imageDatabase);
            logger.LogInformation("....initialization completed.");
        }

        /*
         * Save image metadata in the database.
         */
        public async Task<string> SaveImageInfoAsync(Image image)
        {
            image.Id = Guid.NewGuid().ToString();
            // TODO
            try
            {
                // Create a new item in the Cosmos DB container
                await imageDbContainer.CreateItemAsync(image, new PartitionKey(image.UserId));
                return image.Id; // Return the image ID after successful save
            }
            catch (CosmosException ex)
            {
                logger.LogError($"Cosmos DB error occurred: {ex.Message}");
                return null; // In case of error, return null or handle as needed
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred: {ex.Message}");
                return null; // In case of any other exception, return null or handle as needed
            }
            return null;

        }

        public async Task<Image> GetImageInfoAsync(string userId, string imageId)
        {
            return await imageDbContainer.ReadItemAsync<Image>(imageId, new PartitionKey(userId));
        }

        public async Task<IList<Image>> GetAllImagesInfoAsync()
        {
            List<Image> results = new List<Image>();
            var iterator = imageDbContainer.GetItemLinqQueryable<Image>()
                                           .Where(im => im.Valid && im.Approved)
                                           .ToFeedIterator();
            // Iterate over the paged query result.
            while (iterator.HasMoreResults)
            {
                var images = await iterator.ReadNextAsync();
                // Iterate over a page in the query result.
                foreach (Image image in images)
                {
                    results.Add(image);
                }
            }
            return results;
        }

        public async Task<IList<Image>> GetImageInfoByUserAsync(ApplicationUser user)
        {
            List<Image> results = new List<Image>();
           // var query = imageDbContainer.GetItemLinqQueryable<Image>()
              //                          .WithPartitionKey<Image>(user.Id)
                            //            .Where(im => im.Valid && im.Approved);
            // TODO complete this
            try
            {
                var query = imageDbContainer.GetItemLinqQueryable<Image>()
                                            .WithPartitionKey<Image>(user.Id)
                                            .Where(im => im.Valid && im.Approved)
                                            .ToFeedIterator();

                // Iterate over the query results
                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    foreach (var image in response)
                    {
                        if(image.UserId== user.Id)
                        results.Add(image);
                    }
                }
            }
            catch (CosmosException ex)
            {
                logger.LogError($"Cosmos DB error occurred: {ex.Message}");
                // Handle the exception as needed, possibly rethrow or return an empty list
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred: {ex.Message}");
                // Handle the exception as needed, possibly rethrow or return an empty list
            }

            
            return results;
        }

        public async Task UpdateImageInfoAsync(Image image)
        {
            await imageDbContainer.ReplaceItemAsync<Image>(image, image.Id, new PartitionKey(image.UserId));
        }

        /*
         * Remove both image files and their metadata records in the database.
         */
        public async Task RemoveImagesAsync(ApplicationUser user)
        {
            var query = imageDbContainer.GetItemLinqQueryable<Image>().WithPartitionKey<Image>(user.Id);
            var iterator = query.ToFeedIterator();
            while (iterator.HasMoreResults)
            {
                var images = await iterator.ReadNextAsync();
                foreach (Image image in images)
                {
                    await RemoveImageAsync(image);
                }
            }
            /*
             * Not available?
             * await imageDbContainer.DeleteAllItemsByPartitionKeyStreamAsync(new PartitionKey(image.UserId))
             */
        }

        public async Task RemoveImageAsync(Image image)
        {
            try
            {
                await RemoveImageFileAsync(image);
                await imageDbContainer.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.UserId));
            }
            catch (Azure.RequestFailedException e)
            {
                logger.LogError("Exception while removing blob image: ", e.StackTrace);
            }
        }


        /**
         * The name of a blob containing a saved image (imageId is key for metadata record).
         */
        protected static string BlobName(string userId, string imageId)
        {
            return "image-" + imageId + ".jpg";
        }

        protected string BlobUri(string userId, string imageId)
        {
            return blobContainerClient.Uri + "/" + BlobName(userId, imageId);
        }

        public async Task SaveImageFileAsync(IFormFile imageFile, string userId, string imageId)
        {
            logger.LogInformation("Saving image with id {0} to blob storage", imageId);

            BlobHttpHeaders headers = new BlobHttpHeaders();
            headers.ContentType = "image/jpeg";

            /*
             * TODO upload data to blob storage
             * 
             * Tip: You need to reset the stream position to the beginning before uploading:
             * See https://stackoverflow.com/a/47611795.
             */

            logger.LogInformation($"Saving image with id {imageId} to blob storage");

            BlobClient blobClient = blobContainerClient.GetBlobClient(BlobName(userId, imageId));

           

            try
            {
                // Reset the stream position to the beginning before uploading
                if (imageFile.Length > 0)
                {
                    imageFile.OpenReadStream().Position = 0;
                    await blobClient.UploadAsync(imageFile.OpenReadStream(), headers);
                    logger.LogInformation($"Image {imageId} uploaded successfully.");
                }
                else
                {
                    logger.LogWarning($"Empty file received for image {imageId}.");
                }
            }
            catch (RequestFailedException ex)
            {
                logger.LogError($"Azure Blob Storage error occurred while uploading: {ex.Message}");
                // Handle exception as needed, possibly rethrow or log the error
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred while uploading the image: {ex.Message}");
                // Handle general exceptions
            }

        }

        protected async Task RemoveImageFileAsync(Image image)
        {
            BlobClient blobClient = blobContainerClient.GetBlobClient(BlobName(image.UserId, image.Id));
            logger.LogInformation("Deleting image blob at URI {0}", blobClient.Uri);
            await blobClient.DeleteAsync();
        }

        public string ImageUri(string userId, string imageId)
        {
            return BlobUri(userId, imageId);
        }

        private bool isOkImage(Image image)
        {
            return image.Valid && image.Approved;
        }
    }
}