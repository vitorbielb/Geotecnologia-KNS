using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Validators
{
    public class PropriedadeArquivoViewModelValidator : AbstractValidator<PropriedadeArquivoViewModel>
    {
        private const int LimiteDeArquivos = 5;
        private readonly ApplicationDbContext _context;

        public PropriedadeArquivoViewModelValidator(ApplicationDbContext context)
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

        private bool MenosArquivosQueLimite(PropriedadeArquivoViewModel model)
        {
            var quantidade = _context.Propriedades
                .AsNoTracking()
                .Where(x => x.Id == model.VinculoId)
                .Select(x => x.Documentos != null ? x.Documentos.Count : 0)
                .FirstOrDefault();

            return quantidade < LimiteDeArquivos;
        }
    }
}