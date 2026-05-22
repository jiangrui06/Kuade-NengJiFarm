var fs = require('fs');
var html = fs.readFileSync('table.html', 'utf8');
var scriptStart = html.indexOf('<script>');
var scriptEnd = html.lastIndexOf('</script>');
var script = html.substring(scriptStart, scriptEnd);

var createAppStart = script.indexOf('createApp({');
var objDepth = 0;
var inStr = false;
var strCh = '';
var maxDepth = 0;

for (var i = createAppStart; i < script.length; i++) {
    var ch = script[i];
    if (inStr) {
        if (ch === strCh && script[i-1] !== '\\') inStr = false;
        continue;
    }
    if (ch === "'" || ch === '"') {
        inStr = true;
        strCh = ch;
        continue;
    }
    if (ch === '{') {
        objDepth++;
        if (objDepth > maxDepth) maxDepth = objDepth;
    }
    if (ch === '}') {
        objDepth--;
        if (objDepth === 0) {
            var remainder = script.substring(i+1, i+50);
            console.log('createApp closes at char', i);
            console.log('Remainder:', JSON.stringify(remainder));
            console.log('Final depth:', objDepth);
            break;
        }
    }
}
console.log('Max object depth:', maxDepth);
console.log('Expected depth at close: 0, got:', objDepth);

// Also check: how many methods properties exist?
var methodsStart = script.indexOf('methods: {');
if (methodsStart > 0) {
    var methodsDepth = 1;
    var inStr2 = false;
    var strCh2 = '';
    for (var j = methodsStart + 10; j < script.length && methodsDepth > 0; j++) {
        var c = script[j];
        if (inStr2) {
            if (c === strCh2 && script[j-1] !== '\\') inStr2 = false;
            continue;
        }
        if (c === "'" || c === '"') {
            inStr2 = true;
            strCh2 = c;
            continue;
        }
        if (c === '{') methodsDepth++;
        if (c === '}') methodsDepth--;
    }
    console.log('Methods block ends at char', j);
    console.log('Methods content ends with:', JSON.stringify(script.substring(j-30, j)));
}
