# Envio de e-mail

Hoje o sistema manda uma mensagem só: o link de redefinição de senha. Sem ela,
quem esquece a senha não tem saída — a tela responde "verifique seu e-mail" e
nada chega.

A configuração fica na seção `Email`. Três modos de entrega:

| `Entrega` | O que faz | Onde usar |
|---|---|---|
| `Nenhuma` | Não envia. Registra no log, com aviso, o conteúdo que teria mandado. | Padrão. Nunca finge que enviou. |
| `Pasta` | Grava a mensagem em disco como `.eml`. | Desenvolvimento — abra o arquivo em qualquer leitor de e-mail. |
| `Smtp` | Envio real. | Produção. |

## Produção

**Nunca** ponha servidor, usuário e senha no `appsettings.json` — ele vai para o
repositório. Use variáveis de ambiente:

```bash
Email__Entrega=Smtp
Email__Servidor=smtp.seuprovedor.com
Email__Porta=587
Email__UsarTls=true
Email__Usuario=<usuario>
Email__Senha=<senha>
Email__RemetenteEndereco=nao-responda@seudominio.com.br
Email__RemetenteNome=KNS Ambiental
```

Ou, em desenvolvimento, `dotnet user-secrets set "Email:Senha" "<senha>"`.

## O que você precisa providenciar

1. **Uma conta num provedor transacional** — SendGrid, Amazon SES, Resend,
   Mailgun, ou o SMTP corporativo que já usarem. Qualquer um serve: a
   implementação é SMTP puro, sem amarração a fornecedor.

2. **Um domínio para o remetente.** Mandar de `@gmail.com` ou de um domínio
   sem autorização cai em spam. O endereço precisa ser de um domínio que
   vocês controlem.

3. **Os registros de DNS** que o provedor pedir — SPF e DKIM, normalmente, e
   DMARC se quiserem endurecer. Sem eles a mensagem sai, mas o destinatário
   pode nunca ver. Isso é configuração de domínio, não do sistema.

Enquanto nada disso existir, deixe em `Nenhuma`: o link de redefinição aparece
no log do servidor com um aviso, e dá para socorrer um usuário manualmente sem
a mensagem parecer enviada.

## Conferindo

Com `Entrega: Pasta`, peça uma redefinição na tela de login e abra o `.eml`
gerado. O ciclo inteiro — pedido, link, nova senha valendo e senha antiga
recusada — foi verificado assim.
