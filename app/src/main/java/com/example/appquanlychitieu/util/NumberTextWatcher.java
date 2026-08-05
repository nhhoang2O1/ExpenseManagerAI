package com.example.appquanlychitieu.util;

import android.text.Editable;
import android.text.TextWatcher;
import android.widget.EditText;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.text.ParseException;
import java.util.Locale;

public class NumberTextWatcher implements TextWatcher {

    private final DecimalFormat df;
    private final DecimalFormat dfnd;
    private final EditText et;
    private boolean hasFractionalPart;
    private int trailingZeroCount;

    public NumberTextWatcher(EditText editText) {
        df = new DecimalFormat("#,###.##");
        df.setDecimalSeparatorAlwaysShown(true);
        dfnd = new DecimalFormat("#,###");
        this.et = editText;
        hasFractionalPart = false;
        
        DecimalFormatSymbols symbols = new DecimalFormatSymbols(Locale.getDefault());
        symbols.setGroupingSeparator('.');
        symbols.setDecimalSeparator(',');
        df.setDecimalFormatSymbols(symbols);
        dfnd.setDecimalFormatSymbols(symbols);
    }

    @SuppressWarnings("unused")
    private static final String TAG = "NumberTextWatcher";

    @Override
    public void afterTextChanged(Editable s) {
        et.removeTextChangedListener(this);

        try {
            int inilen, endlen;
            inilen = et.getText().length();

            String v = s.toString().replace(String.valueOf(df.getDecimalFormatSymbols().getGroupingSeparator()), "");
            Number n = df.parse(v);
            int cp = et.getSelectionStart();
            if (hasFractionalPart) {
                StringBuilder trailingZeros = new StringBuilder();
                while (trailingZeroCount-- > 0)
                    trailingZeros.append('0');
                et.setText(new StringBuilder(df.format(n)).append(trailingZeros).toString());
            } else {
                et.setText(dfnd.format(n));
            }

            endlen = et.getText().length();
            int sel = (cp + (endlen - inilen));
            if (sel > 0 && sel <= et.getText().length()) {
                et.setSelection(sel);
            } else {
                
                et.setSelection(et.getText().length() - 1);
            }
        } catch (NumberFormatException | ParseException nfe) {
            
        }

        et.addTextChangedListener(this);
    }

    @Override
    public void beforeTextChanged(CharSequence s, int start, int count, int after) {
    }

    @Override
    public void onTextChanged(CharSequence s, int start, int before, int count) {
        int index = s.toString().indexOf(String.valueOf(df.getDecimalFormatSymbols().getDecimalSeparator()));
        hasFractionalPart = index >= 0;
        if (index > -1) {
            String substring = s.toString().substring(index + 1);
            trailingZeroCount = 0;
            for (char c : substring.toCharArray()) {
                if (c == '0')
                    trailingZeroCount++;
            }
        } else {
            trailingZeroCount = 0;
        }
    }
}
