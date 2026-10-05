// Cadastro de propriedade por número do CAR.
// O desenho manual de polígono foi descontinuado: o perímetro vem da base pública.

let mapaCar = null;
let camadasCar = [];

$(document).ready(function () {
    $('#btn-consultar-car').on('click', consultarCar);

    $('#CodigoCar').on('keydown', function (event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            consultarCar();
        }
    });

    // Trocar o CAR invalida o que foi consultado antes.
    $('#CodigoCar').on('input', function () {
        $('#car-resultado').addClass('d-none');
        $('#btn-cadastrar').prop('disabled', true);
    });
});

function consultarCar() {
    const codigoCar = $('#CodigoCar').val();

    if (!codigoCar) {
        exibirMensagem('warning', 'Informe o número do CAR.');
        return;
    }

    const botao = $('#btn-consultar-car');
    botao.prop('disabled', true);
    exibirMensagem('info', 'Consultando a base do CAR...');

    $.ajax({
        url: '/Propriedades/ConsultarCar',
        type: 'GET',
        data: { codigoCar: codigoCar },
        dataType: 'json'
    })
        .done(function (resposta) {
            if (!resposta.sucesso) {
                exibirMensagem('warning', resposta.mensagem);
                $('#car-resultado').addClass('d-none');
                $('#btn-cadastrar').prop('disabled', true);
                return;
            }

            preencherResultado(resposta);
        })
        .fail(function () {
            exibirMensagem('danger', 'Não foi possível consultar a base do CAR.');
        })
        .always(function () {
            botao.prop('disabled', false);
        });
}

function preencherResultado(imovel) {
    exibirMensagem('success', imovel.mensagem);

    $('#CodigoCar').val(imovel.codigoCar);
    $('#car-municipio').text(formatarTexto(imovel.municipio) + ' / ' + formatarTexto(imovel.uf));
    $('#car-situacao').text(formatarTexto(imovel.situacao));
    $('#car-centro').text(imovel.centroLat.toFixed(6) + ', ' + imovel.centroLng.toFixed(6));

    const area = imovel.areaCalculadaHa || imovel.areaHa;
    $('#car-area').text(area ? area.toLocaleString('pt-BR', { maximumFractionDigits: 2 }) : '-');

    const carregadaEm = imovel.baseCarregadaEm
        ? new Date(imovel.baseCarregadaEm).toLocaleDateString('pt-BR')
        : 'data não registrada';
    $('#car-origem').text(formatarTexto(imovel.origem) + ' — carga de ' + carregadaEm);

    $('#car-resultado').removeClass('d-none');
    $('#btn-cadastrar').prop('disabled', false);

    desenharPerimetro(imovel);
}

function desenharPerimetro(imovel) {
    if (!window.googleMapsApiKey || !document.getElementById('map')) {
        return;
    }

    window.initMapCar = function () {
        const centro = { lat: imovel.centroLat, lng: imovel.centroLng };

        if (mapaCar === null) {
            mapaCar = new google.maps.Map(document.getElementById('map'), {
                zoom: 13,
                center: centro,
                disableDefaultUI: true,
                mapTypeId: 'terrain'
            });

            if (window.knsTemaComponentes) { window.knsTemaComponentes.registrarMapa(mapaCar); }
        } else {
            mapaCar.setCenter(centro);
        }

        camadasCar.forEach(function (camada) { camada.setMap(null); });
        camadasCar = [];

        // O desenho é o mesmo da tela de detalhe, e mora num arquivo só: eram
        // duas cópias, e o defeito de geometria crua estava nas duas.
        if (window.knsPerimetro.desenhar(mapaCar, imovel.perimetro)) {
            return;
        }

        // Sem perímetro desenhável, ao menos marca onde o imóvel fica — o
        // mapa vazio e centrado em lugar nenhum não diz nada a quem cadastra.
        mapaCar.data.forEach(function (feature) { mapaCar.data.remove(feature); });

        camadasCar.push(new google.maps.Marker({
            position: centro,
            map: mapaCar,
            title: imovel.codigoCar
        }));
    };

    if (window.google && window.google.maps) {
        window.initMapCar();
        return;
    }

    if (document.getElementById('google-maps-script')) {
        return;
    }

    const script = document.createElement('script');
    script.id = 'google-maps-script';
    script.src = 'https://maps.googleapis.com/maps/api/js?key=' +
        encodeURIComponent(window.googleMapsApiKey) + '&callback=initMapCar&v=weekly';
    script.defer = true;
    document.head.appendChild(script);
}

function exibirMensagem(tipo, texto) {
    $('#car-mensagem').html('<div class="alert alert-' + tipo + '">' + texto + '</div>');
}

function formatarTexto(valor) {
    return valor ? valor : '-';
}
