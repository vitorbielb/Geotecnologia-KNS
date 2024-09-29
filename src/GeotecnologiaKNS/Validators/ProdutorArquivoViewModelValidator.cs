using FluentValidation;

namespace GeotecnologiaKNS.Validators
{
    public class ProdutorArquivoViewModelValidator : AbstractValidator<ProdutorArquivoViewModel>
    {
        private const int LimiteDeArquivos = 5;
        private readonly ApplicationDbContext context;

        public ProdutorArquivoViewModelValidator(ApplicationDbContext context)
        {
            this.context = context;

            RuleFor(arquivo => arquivo)
                .Must(MenosArquivosQueLimite)
                .WithMessage("Limite de arquivos atingido.");

            RuleFor(x => x.Descricao).NotEmpty();
            RuleFor(x => x.Dados).NotEmpty();
        }

        private bool MenosArquivosQueLimite(ProdutorArquivoViewModel arg)
        {
            var documentos = context.Produtores
                                    .Include(x => x.Documentos)
                                    .First(x => x.Id == arg.VinculoId)
                                    .Documentos!
                                    .ToList();

            return documentos.Count() <= LimiteDeArquivos;
        }
    }
}
