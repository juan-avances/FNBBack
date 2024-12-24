using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Domain.MainModule.Entities.S4Hanna;
using Domain.MainModule.Interfaces.RepositoryContracts.Main;
using Infraestructure.Crosscutting;
using Infraestructure.Crosscutting.Exceptions;
using Infraestructure.Crosscutting.Trazabilidad;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace Infraestructure.Data.Core.Repository
{
    public class S4hanaRepository : IS4HanaRepository
    {
        private readonly IItemTablaGeneralRepository _itemTablaGeneralRepository;
        private readonly ILogger _logger;

        public S4hanaRepository(IItemTablaGeneralRepository itemTablaGeneralRepository,
            IServiceProvider serviceProvider)
        {
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            _logger = Log.Logger;
            _itemTablaGeneralRepository = itemTablaGeneralRepository;

            if (httpContextAccessor.HttpContext != null && httpContextAccessor.HttpContext.User.Claims.Any())
            {
                UsuarioActual = httpContextAccessor.HttpContext.User.Claims?.ElementAt(0).Value;
            }
        }

        public string UsuarioActual { get; }

        public async Task<ProcesoAnulacionResponse> AnularFinanciamiento(ProcesoAnulacionRequest request)
        {
            string guid = Guid.NewGuid().ToString();
            _logger.Information($"SGCMT - Anular Nro PV {request.NumeroPedido}, Contrato {request.CuentaContrato}");

            TrazabilidadLogger.Registrar(guid, "ANULACION FINANCIAMIENTO REQUEST", UsuarioActual, request);

            var response =
                await PostAsync<ProcesoAnulacionRequest, ProcesoAnulacionResponse>(request,
                    UrlS4HanaConst.UrlAnualFinanciamiento);
            TrazabilidadLogger.Registrar(guid, "ANULACION FINANCIAMIENTO RESPONSE", UsuarioActual, response);

            if (response.MensajesRespuesta == null)
            {
                _logger.Information(
                    $"SGCMT - Error Se intentó anular el financiamiento {request.NumeroPedido} pero no se tuvo respuesta del servicio");
                throw new ControlledException(
                    $"Ha ocurrido un problema en el servicio, no se puede anular el financiamiento {request.NumeroPedido}");
            }

            return response;
        }

        public async Task<ProcesoEntregaResponse> ProcesarEntregaPedido(ProcesoEntregaRequest request)
        {
            string guid = Guid.NewGuid().ToString();
            TrazabilidadLogger.Registrar(guid, "PROCESAR ENTREGA REQUEST", UsuarioActual, request);
            var response =
                await PostAsync<ProcesoEntregaRequest, ProcesoEntregaResponse>(request,
                    UrlS4HanaConst.UrlProcesarEntregaPedido);

            TrazabilidadLogger.Registrar(guid, "PROCESAR ENTREGA RESPONSE", UsuarioActual, response);

            return response;
        }

        public async Task<CrearPedidoResponse> AgregarFinanciamiento(CrearPedidoRequest request)
        {
            string guid = Guid.NewGuid().ToString();
            _logger.Information(
                $"Se invoca a FNBPROCESAPEDIDO con el contrato: {request.CuentaContrato} para la sede {request.CodigoSede}");
            TrazabilidadLogger.Registrar(guid, "REGISTRO FINANCIAMIENTO REQUEST", UsuarioActual, request);
            var response =
                await PostAsync<CrearPedidoRequest, CrearPedidoResponse>(request,
                    UrlS4HanaConst.UrlAgregarFinanciamiento);

            TrazabilidadLogger.Registrar(guid, "REGISTRO FINANCIAMIENTO RESPONSE", UsuarioActual, response);

            return response;
        }

        public async Task<EvaluacionClienteResponse> ConsultaCliente(EvaluacionClienteRequest request)
        {
            string guid = Guid.NewGuid().ToString();
            TrazabilidadLogger.Registrar(guid, "CONSULTA CLIENTE REQUEST", UsuarioActual, request);
            var response =
                await PostAsync<EvaluacionClienteRequest, EvaluacionClienteResponse>(request,
                    UrlS4HanaConst.UrlEvaluacionCliente);

            TrazabilidadLogger.Registrar(guid, "CONSULTA CLIENTE RESPONSE", UsuarioActual, response);

            return response;
        }


        public async Task<ConsultClientResponse> GetConsultClient(ConsultClientRequest request)
        {
            string guid = Guid.NewGuid().ToString();
            //SapLogger.Registrar(request, $"ID[{guid}] GetConsultClient REQUEST => ({UsuarioActual})");
            var response =
                await PostAsync<ConsultClientRequest, ConsultClientResponse>(request, UrlS4HanaConst.UrlConsultarCliente);
            //SapLogger.Registrar(response, $"ID[{guid}] GetConsultClient RESPONSE => ({UsuarioActual})");
            return response;
        }

        #region Private methods

        private async Task<TResult> PostAsync<T, TResult>(T request, string detailUrlKey,
            bool isCamelCaseProperty = false)
        {
            var httpClient = await GetHttpClient();
            var stringContent = GetStringContent(request, isCamelCaseProperty);
            string url = GetUrl(UrlS4HanaConst.RootUrl, detailUrlKey);

            _logger.Information($"Request => {stringContent.JsonSerializeObject} - URL => {url}");

            var response = await httpClient.PostAsync(url, stringContent.StringContent);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(response.ReasonPhrase);
            }

            var content = await response.Content.ReadAsStringAsync();

            _logger.Information(
                $"Response => {content} - Request => {stringContent.JsonSerializeObject} - URL => {url}");

            var rpt = JsonConvert.DeserializeObject<TResult>(content);

            return rpt;
        }

        private async Task<HttpClient> GetHttpClient()
        {
            var credentials = await _itemTablaGeneralRepository.GetCredentialsUrl(UrlS4HanaConst.GenericCredentials,
                UrlS4HanaConst.UrlCredentials);

            HttpClientHandler clientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            HttpClient httpClient = new HttpClient(clientHandler);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credentials.User}:{credentials.Password}")));

            return httpClient;
        }

        private (StringContent StringContent, string JsonSerializeObject) GetStringContent<T>(T request, bool isCamelCaseProperty)
        {
            string jsonSerializeObject;

            if (isCamelCaseProperty)
            {
                jsonSerializeObject = JsonConvert.SerializeObject(request, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                });
            }
            else
            {
                jsonSerializeObject = JsonConvert.SerializeObject(request);
            }

            var stringContent = new StringContent(jsonSerializeObject, Encoding.UTF8, "application/json");

            return (stringContent, jsonSerializeObject);
        }

        private string GetUrl(string masterKey, string detailKey)
        {
            var detail = _itemTablaGeneralRepository
                .Find(p => p.TablaGeneral.Nombre == masterKey && p.Nombre == detailKey && p.Estado == 1).First();

            return detail.Valor;
        }

        #endregion
    }
}