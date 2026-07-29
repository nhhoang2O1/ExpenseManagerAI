package com.example.appquanlychitieu.ui.auth;

import android.content.Intent;
import android.os.Bundle;
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
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
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
        String password = textOf(etPassword);
        String confirmPassword = textOf(etConfirmPassword);

        if (name.isEmpty()) {
            layoutName.setError(getString(R.string.invalid_name));
            etName.requestFocus();
            return;
        }
        if (!android.util.Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
            layoutEmail.setError(getString(R.string.invalid_email));
            etEmail.requestFocus();
            return;
        }
        if (password.length() < 8) {
            layoutPassword.setError(getString(R.string.invalid_password_length));
            etPassword.requestFocus();
            return;
        }
        if (!password.equals(confirmPassword)) {
            layoutConfirmPassword.setError(getString(R.string.password_mismatch));
            etConfirmPassword.requestFocus();
            return;
        }

        layoutName.setError(null);
        layoutEmail.setError(null);
        layoutPassword.setError(null);
        layoutConfirmPassword.setError(null);
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
        EditText code = new EditText(this);
        code.setHint("Ma xac thuc 6 so");
        code.setInputType(android.text.InputType.TYPE_CLASS_NUMBER);
        new MaterialAlertDialogBuilder(this)
                .setTitle("Xac thuc email")
                .setMessage("Ma xac thuc da duoc gui toi " + email)
                .setView(code)
                .setPositiveButton("Xac nhan", (dialog, which) -> confirmRegistration(
                        email, name, code.getText().toString().trim()))
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    private void confirmRegistration(String email, String name, String code) {
        if (!code.matches("[0-9]{6}")) {
            Toast.makeText(this, "Nhap ma gom 6 chu so.", Toast.LENGTH_SHORT).show();
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

    private long stableCacheUserId(String identity) {
        long hash = identity == null ? 1L : identity.hashCode();
        return Math.max(1L, Math.abs(hash));
    }

    private void setLoading(boolean loading) {
        btnRegister.setEnabled(!loading);
        progressAuth.setVisibility(loading ? android.view.View.VISIBLE : android.view.View.GONE);
    }
}
