// Ajustes de tela que o CSS sozinho nao resolve. Sem biblioteca.
(function () {
    const raiz = document.documentElement;
    const area = window.visualViewport;

    // A area visivel de verdade, que encolhe quando o teclado do celular abre. O modal usa
    // essas medidas para ficar inteiro acima do teclado, com o botao de salvar a vista.
    function medir() {
        if (!area) {
            return;
        }

        raiz.style.setProperty('--vv-altura', area.height + 'px');
        raiz.style.setProperty('--vv-topo', area.offsetTop + 'px');

        // Zoom de pinca tambem encolhe a area visivel; so conta como teclado sem zoom.
        const teclado = area.scale <= 1.01 && window.innerHeight - area.height > 150;
        raiz.classList.toggle('teclado-aberto', teclado);
    }

    if (area) {
        area.addEventListener('resize', medir);
        area.addEventListener('scroll', medir);
        medir();
    }

    // Campo numerico (dinheiro, juros, parcelas) seleciona tudo ao receber foco: o que se digita
    // substitui o valor, em vez de grudar no fim dele. O setTimeout espera o clique ou toque
    // terminar de posicionar o cursor, que senao desfaria a selecao.
    document.addEventListener('focusin', function (evento) {
        const campo = evento.target;

        if (!(campo instanceof HTMLInputElement) ||
            !campo.matches('[inputmode="decimal"], [inputmode="numeric"], [type="number"]')) {
            return;
        }

        setTimeout(function () {
            if (document.activeElement !== campo) {
                return;
            }

            // setSelectionRange e o que funciona no Safari do iPhone; type=number nao o aceita.
            try {
                campo.setSelectionRange(0, campo.value.length);
            } catch {
                campo.select();
            }
        });
    });

    // Coluna lateral do PC recolhida a so icones. A escolha fica neste navegador; o script no
    // <head> do App.razor a aplica antes da primeira pintura, para a coluna nao abrir e fechar.
    document.addEventListener('click', function (evento) {
        if (!evento.target.closest('[data-recolher]')) {
            return;
        }

        const recolhida = raiz.classList.toggle('lateral-recolhida');

        try {
            localStorage.setItem('lateral-recolhida', recolhida ? '1' : '0');
        } catch {
            // Sem armazenamento (janela privada, por exemplo): vale ate recarregar.
        }
    });

    window.minhasFinancas = {
        rolarAte(id) {
            const alvo = document.getElementById(id);

            if (!alvo) {
                return;
            }

            const suave = !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            alvo.scrollIntoView({ block: 'start', behavior: suave ? 'smooth' : 'auto' });
        }
    };
})();
