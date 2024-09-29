using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Validators
{
    public class ProdutorArquivoViewModelValidator : AbstractValidator<ProdutorArquivoViewModel>
    {
        private const int LimiteDeArquivos = 5;
        private readonly ApplicationDbContext _context;

        public ProdutorArquivoViewModelValidator(ApplicationDbContext context)
        {
            _context = context;

            RuleFor(x => x)
                .Must(MenosArquivosQueLimite)
                .WithMessage("Limite de arquivos atingido.");

            RuleFor(x => x.Descricao)
                .NotEmpty()
                .WithMessage("Descrição é obrigatória.");

            RuleFor(x => x.Dados)
                .NotEmpty()
                .WithMessage("Arquivo é obrigatório.");
        }

        private bool MenosArquivosQueLimite(ProdutorArquivoViewModel model)
        {
            var quantidade = _context.Produtores
                .AsNoTracking()
                .Where(x => x.Id == model.VinculoId)
                .Select(x => x.Documentos!.Count)
                .FirstOrDefault();

            return quantidade < LimiteDeArquivos;
        }
    }
}