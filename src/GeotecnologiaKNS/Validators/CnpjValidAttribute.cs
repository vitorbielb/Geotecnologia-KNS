using System.ComponentModel.DataAnnotations;

namespace GeotecnologiaKNS.Validators
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class CnpjValidAttribute : ValidationAttribute
    {
        private static readonly int[] Weight1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        private static readonly int[] Weight2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        public CnpjValidAttribute() : base("CNPJ inválido")
        {
        }

        public override bool IsValid(object? value)
        {
            return ValidateCnpj(value?.ToString());
        }

        public static bool ValidateCnpj(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return false;

            cnpj = RemoveNonDigits(cnpj);

            if (cnpj.Length != 14)
                return false;

            if (TodosOsDigitosIguais(cnpj))
                return false;

            var digit1 = CalcularDigito(cnpj, Weight1, 12);
            var digit2 = CalcularDigito(cnpj, Weight2, 13);

            return cnpj[12] - '0' == digit1 &&
                   cnpj[13] - '0' == digit2;
        }

        private static int CalcularDigito(string cnpj, int[] pesos, int comprimento)
        {
            var soma = 0;

            for (var i = 0; i < comprimento; i++)
            {
                soma += (cnpj[i] - '0') * pesos[i];
            }

            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        private static string RemoveNonDigits(string value)
        {
            return new string(value.Where(char.IsDigit).ToArray());
        }

        private static bool TodosOsDigitosIguais(string cnpj)
        {
            return cnpj.All(c => c == cnpj[0]);
        }
    }
}