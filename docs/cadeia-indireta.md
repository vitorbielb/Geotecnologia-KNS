# Cadeia de fornecimento indireto

A fazenda que vende o boi gordo ao frigorífico quase sempre comprou o bezerro de
outra. É na outra que o passivo costuma estar: foi lá que o desmatamento
aconteceu, é lá que o embargo está. **Olhar só o fornecedor direto produz um
laudo limpo sobre uma cadeia que não é.**

É o elo que falta na maior parte do monitoramento de cadeia de carne no Brasil,
e o que o Ministério Público vem cobrando nos TACs.

## Como funciona

Na tela do imóvel, aba **Cadeia indireta**, declare os imóveis que forneceram
animais àquele fornecedor direto:

| Campo | Para que serve |
|---|---|
| Número do CAR | Cruzamento geográfico contra todas as camadas |
| CPF/CNPJ | Consulta às listas restritivas por documento |
| Produtor | Identificação no laudo |
| Origem | De onde veio a informação: GTA, declaração, auditoria |

Na análise seguinte, cada fornecedor declarado passa pela **mesma verificação do
fornecedor direto** — todas as camadas, todas as listas restritivas. O resultado
entra no laudo em duas partes: a regra `IND-001` entre as ocorrências, e uma
seção própria com o detalhe de cada imóvel da cadeia.

### Por que é declarado, e não descoberto

O sistema não tem como saber quem vendeu para quem. Quem conhece a cadeia é o
próprio fornecedor, e a prova documental é a **GTA** (Guia de Trânsito Animal),
emitida pelo órgão estadual. Enquanto esses dados não chegam ao sistema, a
cadeia é informada por quem a conhece.

Isso tem uma consequência que o laudo diz com todas as letras:

> A verificação alcança apenas os fornecedores informados. Fornecedores não
> declarados não foram verificados e não estão refletidos neste resultado.

Um laudo que dissesse "cadeia indireta verificada" sem essa ressalva viraria
documento de defesa com base falsa — e é justamente num questionamento do
Ministério Público que isso apareceria.

### Cadeia não declarada não é cadeia limpa

Sem nenhum fornecedor declarado, a `IND-001` sai como **não avaliada**, e a
seção do laudo diz:

> Nenhum fornecedor indireto foi declarado para este imóvel. A cadeia indireta
> não foi verificada — o que não significa que não exista.

### Sem CPF/CNPJ, a verificação é parcial

Só o cruzamento geográfico acontece. Como **quase metade dos termos de embargo
do IBAMA não tem área delimitada**, é exatamente a parte que o documento
encontraria que fica de fora. A tela marca em destaque quem está sem documento.

## O corte: informativo por padrão

A `IND-001` vem como **informativa**: registra o que encontrou sem alterar o
veredito.

A razão é prática. Quase nenhuma indústria tem a cadeia indireta mapeada hoje.
Subir a regra para alerta faria todo laudo sair como alerta no dia seguinte — e
alerta que aparece em tudo deixa de ser alerta. A informação não some: o laudo
ganha a seção da cadeia e nomeia o que foi encontrado.

Quem leva a cadeia indireta a sério sobe o corte em **Regras de conformidade**.
A partir daí:

- achado na cadeia passa a alertar ou bloquear, conforme o corte;
- **cadeia não declarada passa a derrubar o veredito**, porque a regra deixa de
  ser informativa e a sua ausência passa a contar como verificação faltante.

## Limites

- **50 fornecedores por análise.** Cada um custa um cruzamento geoespacial
  completo, e uma cadeia declarada com centenas travaria a fila atrás de uma
  solicitação só. O excedente aparece no laudo como não verificado.
- **Imóvel fora da base do CAR** sai como `NÃO VERIFICADO`, com o motivo — não
  como limpo. A diferença entre os dois é o que o laudo existe para registrar.
- A profundidade é **um nível**. Quem forneceu ao fornecedor indireto não é
  verificado.

## Regras novas chegam a quem já personalizou

A `IND-001` revelou um problema mais geral: indústria que já tinha política
própria nunca recebia regra nova do protocolo — a política é uma cópia feita no
dia da adoção.

Agora toda regra nova entra na política da indústria ao abrir a tela de regras,
sempre **como informativa**, mesmo que o protocolo a traga mais severa. Regra
que ninguém lá dentro revisou pode informar, não decidir. A tela avisa quantas
entraram e pede revisão.

Regra que a indústria **desativou** não volta: a comparação considera também as
desligadas, senão o protocolo ressuscitaria o que alguém decidiu desligar.
