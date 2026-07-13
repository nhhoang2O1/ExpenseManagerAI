package com.example.appquanlychitieu.ui.reminder;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MediatorLiveData;

import com.example.appquanlychitieu.data.model.Reminder;
import com.example.appquanlychitieu.data.repository.RemoteReminderRepository;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;

import java.util.List;

public class ReminderViewModel extends AndroidViewModel {
    private final RemoteReminderRepository remoteRepository;
    private final MediatorLiveData<List<Reminder>> remoteReminders = new MediatorLiveData<>();

    public ReminderViewModel(@NonNull Application application) {
        super(application);
        remoteRepository = new RemoteReminderRepository(application);
    }

    public LiveData<List<Reminder>> getReminders(long userId) {
        refresh(userId);
        return remoteReminders;
    }

    public void insert(Reminder reminder) {
        insert(reminder, null);
    }

    public void insert(Reminder reminder, RemoteCallback<Reminder> callback) {
        remoteRepository.create(reminder, new ForwardingReminderCallback(callback, reminder.getUserId()));
    }

    public void update(Reminder reminder) {
        remoteRepository.update(reminder, new ForwardingReminderCallback(null, reminder.getUserId()));
    }

    public void delete(Reminder reminder) {
        remoteRepository.delete(reminder, new RemoteCallback<Void>() {
                @Override
                public void onSuccess(Void value) {
                    refresh(reminder.getUserId());
                }

                @Override
                public void onError(ApiError error) {
                }
            });
    }

    public void refresh(long userId) {
        remoteRepository.getReminders(userId, new RemoteCallback<List<Reminder>>() {
            @Override
            public void onSuccess(List<Reminder> value) {
                remoteReminders.postValue(value);
            }

            @Override
            public void onError(ApiError error) {
                remoteReminders.postValue(new java.util.ArrayList<>());
            }
        });
    }

    private class ForwardingReminderCallback implements RemoteCallback<Reminder> {
        private final RemoteCallback<Reminder> callback;
        private final long userId;

        ForwardingReminderCallback(RemoteCallback<Reminder> callback, long userId) {
            this.callback = callback;
            this.userId = userId;
        }

        @Override
        public void onSuccess(Reminder value) {
            refresh(userId);
            if (callback != null) callback.onSuccess(value);
        }

        @Override
        public void onError(ApiError error) {
            if (callback != null) callback.onError(error);
        }
    }
}
