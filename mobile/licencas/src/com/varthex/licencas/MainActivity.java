package com.varthex.licencas;

import android.app.*;
import android.os.Bundle;
import android.content.*;
import android.graphics.Color;
import android.text.InputType;
import android.view.*;
import android.widget.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.PrivateKey;
import java.time.*;
import java.time.format.DateTimeFormatter;

public class MainActivity extends Activity {
    private PrivateKey signingKey;
    private EditText client, computer, output;
    private TextView keyStatus, summary, message;
    private Spinner plan;
    private Button date, generate, copy, share;
    private CheckBox authorized;
    private LocalDate start = LocalDate.now(ZoneOffset.ofHours(-3));
    private String generated = "";
    private static final int IMPORT_KEY = 20;
    private static final DateTimeFormatter DISPLAY = DateTimeFormatter.ofPattern("dd/MM/yyyy");
    private final int graphite = Color.rgb(35,37,42);

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);
        getWindow().setStatusBarColor(graphite);
        getWindow().setNavigationBarColor(graphite);
        ScrollView scroll = new ScrollView(this);
        LinearLayout body = new LinearLayout(this);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setPadding(dp(22), dp(22), dp(22), dp(28));
        body.setBackgroundColor(Color.rgb(255,249,242));
        scroll.addView(body);
        setContentView(scroll);
        text(body,"Varthex • Licenças",26);
        text(body,"Gere uma chave de ativação sem internet.",15);
        keyStatus = text(body,"Chave do emissor não importada",14);
        button(body,"Importar chave privada (.pem)",v -> {
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.setType("*/*"); intent.addCategory(Intent.CATEGORY_OPENABLE);
            startActivityForResult(intent, IMPORT_KEY);
        });
        button(body,"Colar chave privada",v -> pastePrivateKey());
        button(body,"Bloquear emissor",v -> { signingKey=null; keyStatus.setText("Emissor bloqueado. Importe ou cole a chave para emitir."); clearResult(); });
        text(body,"Cliente ou estabelecimento",14);
        client = input(body,"Ex.: Lanchonete Exemplo",false);
        client.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(200)});
        text(body,"Código do computador",14);
        computer = input(body,"Cole os 64 caracteres exibidos no Windows",true);
        computer.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
        text(body,"Validade",14);
        plan = new Spinner(this);
        ArrayAdapter<String> adapter = new ArrayAdapter<>(this,android.R.layout.simple_spinner_item,new String[]{"1 mês","2 meses","3 meses","Vitalícia"});
        adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item); plan.setAdapter(adapter);
        body.addView(plan,new LinearLayout.LayoutParams(-1,dp(52)));
        date=button(body,"",v -> new DatePickerDialog(this,(picker,y,m,d)-> {start=LocalDate.of(y,m+1,d); refreshDates();},start.getYear(),start.getMonthValue()-1,start.getDayOfMonth()).show());
        summary=text(body,"",14);
        plan.setOnItemSelectedListener(new android.widget.AdapterView.OnItemSelectedListener(){
            public void onItemSelected(android.widget.AdapterView<?> p,View v,int pos,long id){ refreshDates(); }
            public void onNothingSelected(android.widget.AdapterView<?> p){}
        });
        text(body,"Para renovar antecipadamente, selecione o vencimento atual como data inicial. Horário de Brasília (UTC−3).",13);
        authorized=new CheckBox(this); authorized.setText("Pagamento confirmado ou emissão autorizada"); authorized.setTextColor(graphite); body.addView(authorized);
        generate=button(body,"Gerar chave de ativação",v -> generateLicense());
        message=text(body,"",14); message.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        output=input(body,"A chave gerada aparecerá aqui",true); output.setKeyListener(null); output.setTextIsSelectable(true); output.setMinLines(3); output.setMaxLines(6); output.setSaveEnabled(false);
        copy=button(body,"Copiar chave",v -> {
            ((android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("Licença Varthex",generated));
            message.setText("Chave copiada. Cole na tela de ativação do cliente.");
        });
        share=button(body,"Compartilhar chave",v -> {
            Intent intent=new Intent(Intent.ACTION_SEND); intent.setType("text/plain"); intent.putExtra(Intent.EXTRA_TEXT,generated);
            startActivity(Intent.createChooser(intent,"Enviar chave de ativação"));
        });
        text(body,"Uso do responsável. A chave privada fica apenas na memória durante esta sessão; ela não é enviada nem incluída nas licenças.",13);
        text(body,"Desenvolvido pela Varthex",12);
        clearResult(); refreshDates();
        android.text.TextWatcher changes=new android.text.TextWatcher(){
            public void beforeTextChanged(CharSequence s,int a,int c,int f){}
            public void onTextChanged(CharSequence s,int a,int b,int c){ clearResult(); }
            public void afterTextChanged(android.text.Editable e){}
        };
        client.addTextChangedListener(changes); computer.addTextChangedListener(changes);
    }

    private void pastePrivateKey(){
        LinearLayout content=new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(20),dp(8),dp(20),dp(8));
        text(content,"Cole todo o conteúdo do PEM, incluindo BEGIN PRIVATE KEY e END PRIVATE KEY.",14);
        EditText pem=input(content,"Toque e segure para colar a chave",true);
        pem.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD | InputType.TYPE_TEXT_FLAG_MULTI_LINE | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
        pem.setMinLines(3); pem.setMaxLines(6);
        pem.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(32768)});
        TextView error=text(content,"",14);
        error.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Colar chave privada")
                .setView(content).setNegativeButton("Cancelar",null).setPositiveButton("Conferir chave",null).create();
        dialog.setOnDismissListener(d -> pem.setText(""));
        dialog.getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);
        dialog.setOnShowListener(d -> dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            signingKey=null; clearResult();
            try(InputStream expected=getAssets().open("licenca-publica.pem")){
                signingKey=LicenseIssuer.importKey(pem.getText().toString(),readLimited(expected));
                keyStatus.setText("Chave conferida • pronta para emitir");
                dialog.dismiss();
            } catch(Exception e){
                keyStatus.setText("Chave não importada");
                error.setText("Não foi possível conferir a chave. Cole o PEM completo do emissor original.");
            }
        }));
        dialog.show();
    }
    private int months(){ return plan.getSelectedItemPosition()==3?0:plan.getSelectedItemPosition()+1; }
    private void refreshDates(){
        date.setText("Início: "+start.format(DISPLAY));
        summary.setText(months()==0?"Sem vencimento (vitalícia)":"Válida até "+start.plusMonths(months()).format(DISPLAY)+", às 00:00 (exclusive).");
        clearResult();
    }
    private void clearResult(){
        generated="";
        if(output!=null) output.setText("");
        if(copy!=null) copy.setEnabled(false);
        if(share!=null) share.setEnabled(false);
        if(message!=null) message.setText("");
    }
    private void generateLicense(){
        clearResult();
        if(!authorized.isChecked()){message.setText("Confirme a autorização da emissão."); return;}
        try {
            generated=LicenseIssuer.issue(signingKey,computer.getText().toString(),client.getText().toString(),months(),start);
            output.setText(generated); copy.setEnabled(true); share.setEnabled(true);
            message.setText("Licença gerada para "+client.getText().toString().trim()+". "+summary.getText());
            authorized.setChecked(false);
        } catch(Exception e){ message.setText(e.getMessage()==null?"Não foi possível emitir a licença.":e.getMessage()); }
    }
    @Override protected void onActivityResult(int request,int result,Intent data){
        super.onActivityResult(request,result,data);
        if(request!=IMPORT_KEY || result!=RESULT_OK || data==null || data.getData()==null) return;
        signingKey=null; clearResult();
        try(InputStream input=getContentResolver().openInputStream(data.getData()); InputStream expected=getAssets().open("licenca-publica.pem")){
            signingKey=LicenseIssuer.importKey(readLimited(input),readLimited(expected));
            keyStatus.setText("Chave conferida • pronta para emitir");
        } catch(Exception e){keyStatus.setText("Chave não importada"); message.setText(e.getMessage()==null?"Não foi possível ler o arquivo.":e.getMessage());}
    }
    private String readLimited(InputStream stream) throws IOException {
        if(stream==null) throw new IOException("Arquivo não disponível.");
        ByteArrayOutputStream bytes=new ByteArrayOutputStream(); byte[] buffer=new byte[2048]; int n;
        while((n=stream.read(buffer))!=-1){if(bytes.size()+n>32768) throw new IOException("Arquivo maior que o esperado para uma chave PEM.");bytes.write(buffer,0,n);}
        return new String(bytes.toByteArray(),StandardCharsets.UTF_8);
    }
    @Override protected void onDestroy(){signingKey=null; super.onDestroy();}
    private int dp(int v){return Math.round(v*getResources().getDisplayMetrics().density);}
    private TextView text(LinearLayout body,String value,int size){TextView t=new TextView(this);t.setText(value);t.setTextSize(size);t.setTextColor(graphite);t.setPadding(0,dp(10),0,dp(6));body.addView(t);return t;}
    private EditText input(LinearLayout body,String hint,boolean multi){EditText e=new EditText(this);e.setHint(hint);e.setTextSize(16);e.setTextColor(graphite);e.setHintTextColor(Color.DKGRAY);e.setSingleLine(!multi);e.setMinHeight(dp(52));e.setSaveEnabled(false);e.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO);body.addView(e,new LinearLayout.LayoutParams(-1,-2));return e;}
    private Button button(LinearLayout body,String title,View.OnClickListener action){Button b=new Button(this);b.setText(title);b.setAllCaps(false);b.setMinHeight(dp(50));b.setOnClickListener(action);body.addView(b,new LinearLayout.LayoutParams(-1,-2));return b;}
}
