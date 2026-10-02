# Anexos e backup

Os documentos anexados a produtores, imóveis e análises **não ficam mais dentro
do banco**. Ficam no disco, e o banco guarda só o ponteiro.

## Por que mudou

Os anexos moravam numa coluna `varbinary(max)`. Cada backup do SQL Server
carregava junto todo PDF que algum analista já tinha subido — licença
ambiental digitalizada, contrato, foto de vistoria. Com volume de produção o
backup fica lento a ponto de deixar de ser feito, e backup que não se faz é o
mesmo que não ter.

## O que isso muda na operação

> **O backup do banco sozinho deixou de bastar.** A pasta de anexos precisa ser
> copiada junto, e restaurar um sem o outro deixa o sistema com linhas
> apontando para arquivos que não existem.

Por padrão os arquivos vão para `App_Data/arquivos`, sob a raiz da aplicação.
Em produção vale apontar para um volume separado:

```json
{
  "Arquivos": {
    "Raiz": "D:\\dados\\knsambiental\\anexos"
  }
}
```

O caminho de cada arquivo é `{indústria}/{ano-mês}/{identificador}.{extensão}` —
navegável por um humano em dia de incidente, e sem nenhum diretório acumulando
arquivos indefinidamente.

## A migração dos documentos antigos

Acontece sozinha, na primeira subida depois da atualização. Não há passo de
instalação, porque passo de instalação é passo que alguém esquece — e,
esquecido, este deixaria o banco exatamente do tamanho que a mudança existe
para evitar.

É seguro com o sistema no ar:

- cada documento é copiado para o disco **antes** de a coluna ser zerada;
- a leitura aceita os dois formatos enquanto a migração acontece, então não há
  janela em que um documento fique inacessível;
- interromper no meio não perde nada — a próxima subida continua de onde parou,
  porque o critério é a própria coluna ainda preenchida.

Para desligar (por exemplo, para rodar a migração numa janela controlada):

```json
{ "Arquivos": { "MigrarAutomaticamente": false } }
```

A coluna `Dados` continua no esquema, nula, para que nada quebre se algum
documento não tiver sido migrado. Derrubá-la é limpeza para depois de a
migração estar confirmada em produção.

## Limites

Dez megabytes por arquivo. O conteúdo é lido inteiro em memória antes de ir
para o disco, então esse teto também é o que cada envio simultâneo custa de
memória ao servidor.

## Trocar o destino

`IArmazenamentoDeArquivos` tem uma implementação só, em disco. Trocar por S3 ou
Azure Blob é escrever outra e registrá-la no `Program.cs` — nenhum controlador
precisa mudar.
