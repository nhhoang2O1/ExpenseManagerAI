package com.example.appquanlychitieu.ui.auth;

import android.content.Intent;
import android.content.res.ColorStateList;
import android.graphics.Typeface;
import android.os.Bundle;
import android.text.Editable;
import android.text.InputFilter;
import android.text.TextWatcher;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.Window;
import android.view.WindowManager;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import android.widget.EditText;

import androidx.appcompat.app.AppCompatActivity;
import androidx.lifecycle.ViewModelProvider;

import com.example.appquanlychitieu.MainActivity;
import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.AuthResponseDto;
import com.example.appquanlychitieu.util.SessionManager;
import com.example.appquanlychitieu.ui.common.EdgeToEdgeHelper;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.color.MaterialColors;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.shape.MaterialShapeDrawable;
import com.google.android.material.shape.ShapeAppearanceModel;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;

public class RegisterActivity extends AppCompatActivity {
    private TextInputEditText etName;
    private TextInputEditText etEmail;
    private TextInputEditText etPassword;
    private TextInputEditText etConfirmPassword;
    private MaterialButton btnRegister;
    private TextInputLayout layoutName, layoutEmail, layoutPassword, layoutConfirmPassword;
    private android.widget.ProgressBar progressAuth;
    private TextView tvLogin;
    private SessionManager sessionManager;
    private AuthViewModel viewModel;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_register);
        EdgeToEdgeHelper.applySystemBars(findViewById(R.id.root_register));
        sessionManager = new SessionManager(this);
        viewModel = new ViewModelProvider(this).get(AuthViewModel.class);
        etName = findViewById(R.id.et_name);
        etEmail = findViewById(R.id.et_email);
        etPassword = findViewById(R.id.et_password);
        etConfirmPassword = findViewById(R.id.et_confirm_password);
        layoutName = findViewById(R.id.layout_name);
        layoutEmail = findViewById(R.id.layout_email);
        layoutPassword = findViewById(R.id.layout_password);
        layoutConfirmPassword = findViewById(R.id.layout_confirm_password);
        progressAuth = findViewById(R.id.progress_auth);
        btnRegister = findViewById(R.id.btn_register);
        tvLogin = findViewById(R.id.tv_login);
        btnRegister.setOnClickListener(view -> register());
        tvLogin.setOnClickListener(view -> finish());
    }

    private void register() {
        String name = textOf(etName);
        String email = textOf(etEmail);
        // Không trim mật khẩu: khoảng trắng có thể là ký tự hợp lệ của mật khẩu
        // và phải được tính đúng với số ký tự người dùng đã nhập.
        String password = rawTextOf(etPassword);
        String confirmPassword = rawTextOf(etConfirmPassword);

        // Xóa lỗi của lần kiểm tra trước để trạng thái hiển thị luôn phản ánh
        // đúng dữ liệu hiện tại.
        layoutName.setError(null);
        layoutEmail.setError(null);
        layoutPassword.setError(null);
        layoutConfirmPassword.setError(null);

        boolean valid = true;
        EditText firstInvalidInput = null;

        if (name.isEmpty()) {
            layoutName.setError(getString(R.string.invalid_name));
            valid = false;
            firstInvalidInput = etName;
        }
        if (!android.util.Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
            layoutEmail.setError(getString(R.string.invalid_email));
            valid = false;
            if (firstInvalidInput == null) firstInvalidInput = etEmail;
        }
        if (password.length() < 8) {
            layoutPassword.setError(getString(R.string.invalid_password_length));
            valid = false;
            if (firstInvalidInput == null) firstInvalidInput = etPassword;
        }
        if (!password.equals(confirmPassword)) {
            layoutConfirmPassword.setError(getString(R.string.password_mismatch));
            valid = false;
            if (firstInvalidInput == null) firstInvalidInput = etConfirmPassword;
        }

        if (!valid) {
            if (firstInvalidInput != null) firstInvalidInput.requestFocus();
            return;
        }

        setLoading(true);
        viewModel.register(name, email, password, new RemoteCallback<Void>() {
            @Override
            public void onSuccess(Void ignored) {
                setLoading(false);
                showConfirmation(email, name);
            }

            @Override
            public void onError(ApiError error) {
                setLoading(false);
                layoutConfirmPassword.setError(error.getMessage());
            }
        });
    }

    private void showConfirmation(String email, String name) {
        int horizontalPadding = dp(20);
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(horizontalPadding, dp(20), horizontalPadding, dp(12));

        TextView title = new TextView(this);
        title.setText("Xác thực email");
        title.setTextColor(MaterialColors.getColor(content,
                com.google.android.material.R.attr.colorOnSurface));
        title.setTextSize(TypedValue.COMPLEX_UNIT_SP, 22);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        content.addView(title);

        TextView message = new TextView(this);
        message.setText("Mã xác thực đã được gửi tới " + email);
        message.setTextColor(MaterialColors.getColor(content,
                com.google.android.material.R.attr.colorOnSurfaceVariant));
        message.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        LinearLayout.LayoutParams messageParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        messageParams.topMargin = dp(8);
        content.addView(message, messageParams);

        EditText code = new EditText(this);
        code.setHint("Nhập mã xác thực 6 số");
        code.setInputType(android.text.InputType.TYPE_CLASS_NUMBER);
        code.setSingleLine(true);
        code.setFilters(new InputFilter[]{new InputFilter.LengthFilter(6)});
        code.setPadding(dp(12), dp(12), dp(12), dp(12));
        LinearLayout.LayoutParams codeParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        codeParams.topMargin = dp(16);
        content.addView(code, codeParams);

        LinearLayout buttonRow = new LinearLayout(this);
        buttonRow.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);

        MaterialButton cancel = new MaterialButton(this, null,
                com.google.android.material.R.attr.borderlessButtonStyle);
        cancel.setText(R.string.cancel);
        buttonRow.addView(cancel);

        MaterialButton confirm = new MaterialButton(this, null,
                com.google.android.material.R.attr.borderlessButtonStyle);
        confirm.setText("Xác nhận");
        confirm.setEnabled(false);
        LinearLayout.LayoutParams confirmParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        confirmParams.setMarginStart(dp(12));
        buttonRow.addView(confirm, confirmParams);

        LinearLayout.LayoutParams buttonRowParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        buttonRowParams.topMargin = dp(16);
        content.addView(buttonRow, buttonRowParams);

        androidx.appcompat.app.AlertDialog dialog = new MaterialAlertDialogBuilder(this)
                .setView(content, 0, 0, 0, 0)
                .create();
        cancel.setOnClickListener(view -> dialog.dismiss());
        confirm.setOnClickListener(view -> {
            dialog.dismiss();
            confirmRegistration(email, name, code.getText().toString().trim());
        });
        code.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence value, int start, int count, int after) { }

            @Override public void onTextChanged(CharSequence value, int start, int before, int count) {
                confirm.setEnabled(value != null && value.toString().matches("\\d{6}"));
            }

            @Override public void afterTextChanged(Editable value) { }
        });
        dialog.show();

        Window window = dialog.getWindow();
        if (window != null) {
            MaterialShapeDrawable background = new MaterialShapeDrawable(ShapeAppearanceModel.builder()
                    .setAllCornerSizes(dp(20))
                    .build());
            background.setFillColor(ColorStateList.valueOf(MaterialColors.getColor(content,
                    com.google.android.material.R.attr.colorSurface)));
            window.setBackgroundDrawable(background);
            window.setLayout((int) (getResources().getDisplayMetrics().widthPixels * 0.9f),
                    WindowManager.LayoutParams.WRAP_CONTENT);
        }
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private void confirmRegistration(String email, String name, String code) {
        if (!code.matches("[0-9]{6}")) {
            Toast.makeText(this, "Nhập mã gồm 6 chữ số.", Toast.LENGTH_SHORT).show();
            showConfirmation(email, name);
            return;
        }
        setLoading(true);
        viewModel.confirmRegistration(email, code, new RemoteCallback<AuthResponseDto>() {
            @Override public void onSuccess(AuthResponseDto response) {
                setLoading(false);
                String remoteId = response.resolvedId();
                String resolvedEmail = response.resolvedEmail() == null ? email : response.resolvedEmail();
                String resolvedName = response.resolvedName() == null ? name : response.resolvedName();
                sessionManager.createRemoteLoginSession(
                        stableCacheUserId(remoteId == null ? resolvedEmail : remoteId), remoteId,
                        resolvedName, resolvedEmail, true, response.resolvedToken(),
                        response.resolvedRefreshToken(), response.expiresIn);
                startActivity(new Intent(RegisterActivity.this, MainActivity.class)
                        .setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK));
                finish();
            }
            @Override public void onError(ApiError error) {
                setLoading(false);
                Toast.makeText(RegisterActivity.this, error.getMessage(), Toast.LENGTH_LONG).show();
                showConfirmation(email, name);
            }
        });
    }

    private String textOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }

    private String rawTextOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString();
    }

    private long stableCacheUserId(String identity) {
        long hash = identity == null ? 1L : identity.hashCode();
        return Math.max(1L, Math.abs(hash));
    }

    private void setLoading(boolean loading) {
        btnRegister.setEnabled(!loading);
        progressAuth.setVisibility(loading ? android.view.View.VISIBLE : android.view.View.GONE);
    }
}
