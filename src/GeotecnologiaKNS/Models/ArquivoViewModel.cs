namespace GeotecnologiaKNS.Models
{
    public abstract class ArquivoViewModel<TModel> : Arquivo
    {
        public override int VinculoId { get; set; }

        public abstract TModel Model { get; }

    }
}
