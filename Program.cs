using Azure;
using Azure.AI.AnomalyDetector;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Text;

namespace anomaly_detector_quickstart
{
    internal class Program
    {
        const string dataPath = "AAD_DemoTestValues_JoffreyNurit.csv";

        static void Main()
        {
            #region configuration / startup

            var builder = new ConfigurationBuilder();
            // tell the builder to look for the appsettings.json file
            builder
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<Program>();
            var configuration = builder.Build();

            #endregion

            #region setup client

            var credential = new AzureKeyCredential(configuration["AzureAnomalyDetector:SubscriptionKey"]);
            var endpointUri = new Uri(configuration["AzureAnomalyDetector:Endpoint"]);

            var anomalyDetectorClient = new AnomalyDetectorClient(endpointUri, credential);

            #endregion

            #region Prepare datas

            //Can be improved, but not needed for this exemple
            var datas = File.ReadAllLines(dataPath, Encoding.UTF8)
                .Skip(1)
                .Where(e => e.Trim().Length != 0)
                .Select(e => e.Split(','))
                .Select(e => {
                    float convertedNumber = 0;

                    if (float.TryParse(e[1], out convertedNumber))
                        return e[0] + "," + convertedNumber;

                    return e[0] + ",-1";
                })
                .Select(e => e.Split(','))
                .Select(e => new TimeSeriesPoint(float.Parse(e[1])) { Timestamp = DateTime.Parse(e[0]).ToUniversalTime().Date });

            //Very important : timeseries need to be ordonned BEFORE send to Anomaly detector
            datas = datas.OrderBy(e => e.Timestamp)
                .ToList();

            UnivariateDetectionOptions request = new UnivariateDetectionOptions(datas)
            {
                //Our data is by day
                Granularity = TimeGranularity.Daily,
                //We have a periodicity of 7 days. Often, we have less data on week-end
                Period = 7,
                //More sensibility = more anomaly detected. Is related to Severity score. if ((severity score + sensibility) > 1), anomaly is raise
                Sensitivity = 80
            };

            #endregion

            OneSerieTestLastDot(anomalyDetectorClient, request);
        }

        /// <summary>
        /// Test only last dot for the simple serie
        /// </summary>
        /// <param name="anomalyDetectorClient"></param>
        /// <param name="request"></param>
        private static void OneSerieTestLastDot(AnomalyDetectorClient anomalyDetectorClient, UnivariateDetectionOptions request)
        {
            //We launch test on last dot
            var result = anomalyDetectorClient.DetectUnivariateLastPoint(request);
            var lastDot = request.Series.Last();

            if (result.Value.IsAnomaly)
            {
                Console.WriteLine("Last dot {0}:{1} is an anomaly. Expected by AI : {2}", lastDot.Timestamp.Value.Date, lastDot.Value, result.Value.ExpectedValue);
            }
            else
            {
                Console.WriteLine("Last dot {0}:{1} isn't an anomaly. Expected by AI : {2}", lastDot.Timestamp.Value.Date, lastDot.Value, result.Value.ExpectedValue);
            }
        }
    }
}