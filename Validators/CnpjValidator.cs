using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace GeotecnologiaKNS.Validators
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class CnpjValid : ValidationAttribute
    {
        public string ErrorMessage { get; set; }

        public override string FormatErrorMessage(string name)
        {
            return base.FormatErrorMessage(name);
        }

        public override bool IsValid(object? value)
        {
            return ValidateCNPJ(value?.ToString());
        }

        public static bool ValidateCNPJ(string? cnpj)
        {
            if (cnpj is null)
            {
                return false;
            }

            // Remove any non-numeric characters from the input
            cnpj = Regex.Replace(cnpj, @"[^0-9]", "");

            // Check if the CNPJ has 14 digits
            if (cnpj.Length != 14)
            {
                return false;
            }

            // Calculate the first verification digit
            int[] weight1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum1 = 0;
            for (int i = 0; i < 12; i++)
            {
                sum1 += (cnpj[i] - '0') * weight1[i];
            }
            int remainder1 = sum1 % 11;
            int digit1 = remainder1 < 2 ? 0 : 11 - remainder1;

            // Calculate the second verification digit
            int[] weight2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum2 = 0;
            for (int i = 0; i < 13; i++)
            {
                sum2 += (cnpj[i] - '0') * weight2[i];
            }
            int remainder2 = sum2 % 11;
            int digit2 = remainder2 < 2 ? 0 : 11 - remainder2;

            // Check if the verification digits match
            return cnpj[12] - '0' == digit1 && cnpj[13] - '0' == digit2;
        }
    }
}
