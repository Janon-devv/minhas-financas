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
