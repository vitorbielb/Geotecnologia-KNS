namespace GeotecnologiaKNS.ViewModels
{
    public abstract class ArquivoViewModel<TModel> : Arquivo
    {
        public override int VinculoId { get; set; }

        public abstract TModel Model { get; }

    }
}
