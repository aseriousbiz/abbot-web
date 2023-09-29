// CodeMirror, copyright (c) by Marijn Haverbeke and others
// Distributed under an MIT license: https://codemirror.net/LICENSE
import CodeMirror from "codemirror";

export default function() {
    CodeMirror.defineOption("fullScreen", false, function(cm, val, old) {
        if (old === CodeMirror.Init) old = false;
        if (!old === !val) return;
        if (val) setFullscreen(cm);
        else setNormal(cm);
    });
}

function setFullscreen(cm) {
    const wrap = cm.getWrapperElement();
    cm.state.fullScreenRestore = {
        scrollTop: window.pageYOffset,
        scrollLeft: window.pageXOffset,
        width: wrap.style.width,
        height: wrap.style.height
    };
    wrap.classList.add("CodeMirror-fullscreen");
    document.documentElement.style.overflow = "hidden";
    document.body.classList.add('fullscreen');
    cm.refresh();
}

function setNormal(cm) {
    const wrap = cm.getWrapperElement();
    document.documentElement.style.overflow = "";
    const info = cm.state.fullScreenRestore;
    wrap.style.width = info.width;
    wrap.style.height = info.height;
    wrap.classList.remove("CodeMirror-fullscreen");
    window.scrollTo(info.scrollLeft, info.scrollTop);
    document.body.classList.remove('fullscreen');
    cm.refresh();
}
