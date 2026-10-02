// Alternância entre tema claro e escuro.
//
// A escolha fica em localStorage e é por navegador, não por usuário: é
// preferência de quem está olhando a tela, não dado de conta.
//
// A aplicação inicial NÃO acontece aqui — precisa correr antes da primeira
// pintura, senão a tela pisca em branco antes de escurecer. Esse trecho está
// embutido no <head> do layout.

(function () {
    'use strict';

    var CHAVE = 'kns-tema';

    function temaAtual() {
        return document.documentElement.getAttribute('data-tema') === 'escuro' ? 'escuro' : 'claro';
    }

    function aplicar(tema) {
        document.documentElement.setAttribute('data-tema', tema);

        try {
            localStorage.setItem(CHAVE, tema);
        } catch (e) {
            // Navegação privada ou armazenamento bloqueado: o tema ainda vale
            // nesta página, só não sobrevive à navegação.
        }

        atualizarRotulos(tema);

        // Componentes que pintam a si mesmos (gráficos, mapas) não enxergam a
        // troca de variável CSS e precisam ser avisados.
        document.dispatchEvent(new CustomEvent('kns:tema', { detail: { tema: tema } }));
    }

    function atualizarRotulos(tema) {
        var proximo = tema === 'escuro' ? 'claro' : 'escuro';
        var rotulo = proximo === 'escuro' ? 'Ativar tema escuro' : 'Ativar tema claro';

        Array.prototype.forEach.call(document.querySelectorAll('.kns-tema'), function (botao) {
            botao.setAttribute('title', rotulo);
            botao.setAttribute('aria-label', rotulo);
        });
    }

    function alternar() {
        aplicar(temaAtual() === 'escuro' ? 'claro' : 'escuro');
    }

    document.addEventListener('DOMContentLoaded', function () {
        atualizarRotulos(temaAtual());

        Array.prototype.forEach.call(document.querySelectorAll('.kns-tema'), function (botao) {
            botao.addEventListener('click', alternar);
        });
    });

    // Acompanha a preferência do sistema enquanto o usuário não escolheu nada.
    if (window.matchMedia) {
        var consulta = window.matchMedia('(prefers-color-scheme: dark)');
        var aoMudar = function (evento) {
            var escolhido = null;
            try {
                escolhido = localStorage.getItem(CHAVE);
            } catch (e) { /* sem acesso ao armazenamento */ }

            if (!escolhido) {
                aplicar(evento.matches ? 'escuro' : 'claro');
            }
        };

        if (consulta.addEventListener) {
            consulta.addEventListener('change', aoMudar);
        } else if (consulta.addListener) {
            consulta.addListener(aoMudar);
        }
    }

    window.knsTema = { atual: temaAtual, aplicar: aplicar, alternar: alternar };
})();
