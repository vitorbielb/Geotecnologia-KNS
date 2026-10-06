# Laudo de análise socioambiental

O documento que a indústria arquiva, anexa a contrato e apresenta em auditoria.
Quando o assunto vira questionamento do Ministério Público, é ele que responde.

Sai pelo botão **Baixar laudo** na análise concluída, ou direto em
`/Solicitacoes/Laudo/{id}`.

## Por que está nessa ordem

A ordem das seções não é estética:

1. **Resultado** primeiro, porque é o que se procura.
2. **Identificação** do imóvel, do produtor e do solicitante.
3. **O que não foi verificado** — antes de qualquer ocorrência. Um laudo sem
   achados se lê como aprovação, e é exatamente aí que a omissão engana.
4. **Mapa** com o perímetro e as sobreposições desenhadas.
5. **Ocorrências**, agrupadas por regra, com área, percentual e fundamento.
6. **Cadeia indireta**, dizendo que alcança só o que foi declarado.
7. **Protocolo aplicado** e **bases consultadas** — o lastro de quem conferir.

Nada é recalculado na emissão: tudo vem do que ficou gravado na execução. Um
laudo que mudasse ao ser reimpresso não defenderia ninguém. O retrato da
política é lido do que foi salvo naquele dia, então afrouxar uma regra depois
não reescreve um bloqueio já emitido.

## Código de conferência

Cada laudo traz um código derivado do conteúdo da análise — veredito, datas,
área, retrato da política e ocorrências. Serve para quem recebe o documento
confirmar que ele corresponde a uma análise registrada, e para detectar um PDF
adulterado: o mesmo laudo emitido duas vezes dá o mesmo código, e qualquer
alteração dá outro.

A data de emissão fica **de fora** do cálculo de propósito. Se entrasse,
reemitir o mesmo laudo daria outro código, e o código deixaria de servir para
conferir coisa alguma.

> **Não é assinatura digital**, e o laudo diz isso. Assinatura exigiria
> certificado ICP-Brasil, que é decisão de quem assina, não do sistema.

## O que o laudo recusa afirmar

- **CAR cancelado** ganha aviso destacado. O cadastro foi anulado pelo órgão; a
  análise descreve o perímetro registrado, e o laudo não deixa isso implícito.
- **Cadeia indireta não declarada** aparece como não verificada, nunca como
  limpa.
- **Regra sem base** é listada com o motivo — camada ausente, lista restritiva
  ausente, produtor sem documento ou cadeia não informada. São quatro faltas
  que se resolvem em quatro lugares diferentes.

## O mapa

Perímetro em azul; sobreposições na cor da gravidade. As geometrias vão
codificadas em polilinha porque o endereço da imagem tem limite de tamanho — um
perímetro do CAR em coordenadas soltas estoura sozinho. Medido num imóvel real:
1.097 bytes de GeoJSON viram 157 caracteres na URL.

As sobreposições maiores entram primeiro. Se o endereço chegar ao limite, o que
se perde são as menos relevantes, não as que decidem o laudo.

Falha ao gerar o mapa **não impede a emissão**: o documento sai dizendo que o
mapa não pôde ser gerado. Trocar um laudo incompleto por laudo nenhum seria pior.

## A biblioteca

QuestPDF, licença Community — gratuita abaixo de um milhão de dólares de receita
anual, paga acima disso. Trocar significa reescrever só o `LaudoPdf`; o
levantamento dos dados e o mapa não dependem dela.

Não declare família de fonte no documento. A fonte embarcada na biblioteca
renderiza igual no Windows e no Linux; pedir Calibri ou Arial quebra a emissão
num servidor que não as tenha instaladas, e foi o que aconteceu no primeiro
teste.
