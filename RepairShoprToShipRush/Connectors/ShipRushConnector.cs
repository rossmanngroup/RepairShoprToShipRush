﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using RepairShoprToShipRush.Domain;
using RepairShoprToShipRush.Helpers;

namespace RepairShoprToShipRush.Connectors
{
    class ShipRushConnector : IDisposable
    {
        private HttpClient client;
        private ILogger log;

        public ShipRushConnector(ILogger log)
        {
            this.log = log;

            client = new HttpClient();

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        }

        public void Dispose()
        {
            client.Dispose();
        }

        public async Task<string> AddOrder(string uri, Invoice invoice)
        {
            string lineitemsXml = GetLineItems(Constants.itemPayloadTemplate, invoice);
            string xmlPayload = GetXmlContent(Constants.xmlPayloadTemplate, invoice, lineitemsXml);
            var xmlContent = new StringContent(xmlPayload, Encoding.UTF8, "application/xml");

            log.LogInformation($"{DateTime.Now} | Pushing payload {xmlPayload} to Uri {uri}");

            var response = await client.PostAsync(uri, xmlContent);

            if (response.IsSuccessStatusCode)
            {
                var orderXml = await response.Content.ReadAsStringAsync();
                XmlDocument orderDocument = new XmlDocument();
                orderDocument.LoadXml(orderXml);

                try
                {
                    return orderDocument.GetElementsByTagName("OrderId")[0].InnerText;
                }
                catch (Exception)
                {
                    log.LogWarning($"{DateTime.Now} | The response from ShipRush did not return an OrderId, most likely this order already exists.");
                    return "null-order-id";
                }
            }

            return null;
        }

        private string GetLineItems(string itemPayloadTemplate, Invoice invoice)
        {
            string lineitems = string.Empty;

            foreach (var lineitem in invoice.line_items)
            {
                string itemtotal = (float.Parse(lineitem.quantity) * float.Parse(lineitem.price)).ToString();

                string itemPayload = string.Format(itemPayloadTemplate,
                    lineitem.name,
                    lineitem.price,
                    lineitem.quantity,
                    itemtotal
                    );

                lineitems += itemPayload;
            }

            return lineitems;
        }

        private string GetXmlContent(string xmlPayloadTemplate, Invoice invoice, string lineitems)
        {
            string xmlPayload = string.Format(xmlPayloadTemplate,
                                                    invoice.customer.fullname,
                                                    invoice.customer.business_name,
                                                    invoice.customer.address,
                                                    invoice.customer.address_2,
                                                    invoice.customer.city,
                                                    invoice.customer.state,
                                                    invoice.customer.zip,
                                                    invoice.customer.mobile,
                                                    invoice.customer.email,
                                                    lineitems,
                                                    invoice.number,
                                                    invoice.tax,
                                                    invoice.total,
                                                    invoice.line_items.Count
                                                    );

            log.LogInformation($"{DateTime.Now} | Parsing completed, XML Payload: {xmlPayload}");
            return xmlPayload;
        }
    }
}