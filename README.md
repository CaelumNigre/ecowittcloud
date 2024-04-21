# A simple application to retrieve data from EcoWitt cloud API

## Background
[Ecowitt](https://www.ecowitt.com/shop/homePage) is a brand of weather measurment devices with the ability to upload measurments to Ecowitt cloud. The cloud provides the ability to retrieve the data from your sensors using [Ecowitt cloud API](https://doc.ecowitt.net/web/#/apiv3en?page_id=1).
There is a limitation of the cloud API, that the older data is available with reduced time resolution. While the recent data (less than 90 days old) is available with 5 minutes resolution, very old data can have resolution as low as 1 day.
Therefore in order to preserve a complete, high resolution data from weather station it needs to be downloaded from Ecowitt cloud to your own data store.

## Application concept
The application is designed as a scheduled data pull from Ecowitt cloud API. The data is saved in the cloud storage where it can be later retrieved for use in data analytics and visualizations using variety of tools.

## Technical architecture
The application is designed with following principles:
* Application and data is hosted in [Microsoft Azure cloud](https://azure.com)
* Application uses [Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-overview) for pulling the data
* The data is stored either as raw JSON blobs in Azure Storage Account or in tables of [Azure Storage Tables](https://azure.microsoft.com/en-us/products/storage/tables).
* The configuration parameters of data retrieval (e.g. unit of measurments, data channels, devices) are configurable

The application infrastructure consists of the following elements:
* Function App which hosts data pull application
* Storage Account for Function App - required by Azure Functions to operate
* Storage Account for data - where sensor data is kept either in blobs or in tables
* Azure Key Vault - to store Ecowitt API key and other secrets
* (Optional) Application Insights component and Log Analytics Workspace - to keep logs from application, mostly for debugging purposes

The application is written in C#. 
