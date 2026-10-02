// Suporte de tema para componentes que desenham em canvas ou iframe e, por
// isso, não acompanham a troca das variáveis CSS: gráficos e mapas.

(function (global) {
    'use strict';

    function cor(nome, padrao) {
        try {
            var v = getComputedStyle(document.documentElement).getPropertyValue(nome).trim();
            return v || padrao;
        } catch (e) {
            return padrao;
        }
    }

    function escuro() {
        return document.documentElement.getAttribute('data-tema') === 'escuro';
    }

    // Estilo de mapa escuro, enxuto de propósito: só o necessário para o mapa
    // deixar de ser uma mancha clara no meio de uma tela escura. O polígono do
    // imóvel continua desenhado por cima, com as cores do sistema.
    var MAPA_ESCURO = [
        { elementType: 'geometry', stylers: [{ color: '#0f2230' }] },
        { elementType: 'labels.text.stroke', stylers: [{ color: '#0f2230' }] },
        { elementType: 'labels.text.fill', stylers: [{ color: '#8098ac' }] },
        { featureType: 'administrative', elementType: 'geometry', stylers: [{ color: '#1d3243' }] },
        { featureType: 'landscape.natural', elementType: 'geometry', stylers: [{ color: '#13293a' }] },
        { featureType: 'poi', stylers: [{ visibility: 'off' }] },
        { featureType: 'road', elementType: 'geometry', stylers: [{ color: '#1a3346' }] },
        { featureType: 'road', elementType: 'labels', stylers: [{ visibility: 'off' }] },
        { featureType: 'transit', stylers: [{ visibility: 'off' }] },
        { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#0a1621' }] }
    ];

    var mapas = [];
    var graficos = [];

    function estiloMapa() {
        return escuro() ? MAPA_ESCURO : null;
    }

    function registrarMapa(mapa) {
        if (!mapa) { return; }
        mapas.push(mapa);
        mapa.setOptions({ styles: estiloMapa() });
    }

    function opcoesGrafico() {
        return {
            grade: cor('--kns-linha', '#DDE7EF'),
            texto: cor('--kns-neutro', '#6C8394')
        };
    }

    function registrarGrafico(grafico) {
        if (!grafico) { return; }
        graficos.push(grafico);
    }

    function aplicarNoGrafico(grafico) {
        var o = opcoesGrafico();
        var eixos = grafico.options && grafico.options.scales;

        if (!eixos) { return; }

        (eixos.yAxes || []).forEach(function (eixo) {
            eixo.ticks = eixo.ticks || {};
            eixo.ticks.fontColor = o.texto;
            eixo.gridLines = eixo.gridLines || {};
            eixo.gridLines.color = o.grade;
            eixo.gridLines.zeroLineColor = o.grade;
        });

        (eixos.xAxes || []).forEach(function (eixo) {
            eixo.ticks = eixo.ticks || {};
            eixo.ticks.fontColor = o.texto;
        });

        grafico.update();
    }

    document.addEventListener('kns:tema', function () {
        mapas.forEach(function (m) {
            try { m.setOptions({ styles: estiloMapa() }); } catch (e) { /* mapa descartado */ }
        });

        graficos.forEach(function (g) {
            try { aplicarNoGrafico(g); } catch (e) { /* gráfico descartado */ }
        });
    });

    global.knsTemaComponentes = {
        cor: cor,
        escuro: escuro,
        estiloMapa: estiloMapa,
        opcoesGrafico: opcoesGrafico,
        registrarMapa: registrarMapa,
        registrarGrafico: registrarGrafico
    };
})(window);
